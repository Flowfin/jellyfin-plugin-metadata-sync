using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Guards the manifest fields that reach an operator wrong without any build
/// failing: the version the package is stamped with, the runtime the package
/// claims, the server line it claims to install on, and the owner it names.
/// Each was the project template's value at some point, and none of them is
/// checkable against the manifest alone.
/// </summary>
public class ManifestTests
{
    /// <summary>
    /// The manifest is the single place a release version is written and the
    /// build reads it from there. A version restated in the build lets a plain
    /// build and a packaged build disagree, and the package then carries a
    /// version the catalogue never showed.
    /// </summary>
    [Fact]
    public void StampedVersionIsTheVersionTheManifestDeclares()
    {
        var declared = ManifestField("version");
        var stamped = typeof(Plugin).Assembly.GetName().Version;

        Assert.NotNull(stamped);
        Assert.Equal(declared, stamped.ToString());
    }

    /// <summary>
    /// The framework field tells the server which runtime the assembly needs.
    /// Naming one the project does not build against produces a package the
    /// server accepts and then cannot load.
    /// </summary>
    [Fact]
    public void ManifestFrameworkIsTheOneThePluginProjectTargets()
    {
        var declared = ManifestField("framework");
        var targeted = PluginProjectProperty("TargetFramework");

        Assert.Equal(targeted, declared);
    }

    /// <summary>
    /// The target ABI is the oldest server line the package claims to install
    /// on, and it is only meaningful next to the server packages the plugin
    /// compiles against. An ABI newer than those is a claim the build cannot
    /// support; an ABI older is a promise made to servers whose API the build
    /// never saw.
    /// </summary>
    [Fact]
    public void ManifestTargetAbiAgreesWithTheServerPackagesTheProjectCompilesAgainst()
    {
        var declaredAbi = new Version(ManifestField("targetAbi"));
        var controller = new Version(PluginPackageVersion("Jellyfin.Controller"));
        var model = new Version(PluginPackageVersion("Jellyfin.Model"));

        Assert.Equal(controller.Major, model.Major);
        Assert.Equal(controller.Minor, model.Minor);

        Assert.Equal(controller.Major, declaredAbi.Major);
        Assert.Equal(controller.Minor, declaredAbi.Minor);
    }

    /// <summary>
    /// The owner is the one manifest field an operator reads as provenance, and
    /// the template shipped it naming a project that did not write this plugin.
    /// </summary>
    [Fact]
    public void ManifestOwnerIsNotTheProjectTemplateOwner()
    {
        Assert.NotEqual("jellyfin", ManifestField("owner"), StringComparer.OrdinalIgnoreCase);
    }

    private static string ManifestField(string name)
    {
        var manifest = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "build.yaml"));
        var match = Regex.Match(
            manifest,
            "^" + Regex.Escape(name) + ":[ \t]*\"([^\"]*)\"[ \t]*$",
            RegexOptions.Multiline);

        Assert.True(match.Success, "build.yaml declares no quoted '" + name + "' field.");
        return match.Groups[1].Value;
    }

    private static XDocument PluginProject()
        => XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Jellyfin.Plugin.MetadataSync.csproj"));

    private static string PluginProjectProperty(string name)
    {
        var declared = PluginProject().Descendants(name).Select(e => e.Value.Trim()).FirstOrDefault();

        Assert.False(string.IsNullOrEmpty(declared), "The plugin project declares no <" + name + ">.");
        return declared!;
    }

    private static string PluginPackageVersion(string packageId)
    {
        var declared = PluginProject().Descendants("PackageReference")
            .Where(e => string.Equals(e.Attribute("Include")?.Value, packageId, StringComparison.Ordinal))
            .Select(e => e.Attribute("Version")?.Value)
            .FirstOrDefault();

        Assert.False(string.IsNullOrEmpty(declared), "The plugin project references no " + packageId + " with a version.");
        return declared!;
    }
}
