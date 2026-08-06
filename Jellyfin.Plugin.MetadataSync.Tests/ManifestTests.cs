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

    /// <summary>
    /// The same manifest, once with the line endings a Linux checkout has and
    /// once with the ones a default Windows clone produces. The escapes are
    /// written out rather than taken from a file, because a fixture whose
    /// subject is a carriage return cannot be stored as one: nothing in this
    /// repository keeps a tracked file at CRLF, and an editor or a checkout
    /// would silently delete the byte the fixture exists to carry.
    /// </summary>
    private const string LfManifest = "name: \"Metadata Sync\"\nversion: \"1.2.3.4\"\nframework: \"net9.0\"\n";

    private const string CrlfManifest = "name: \"Metadata Sync\"\r\nversion: \"1.2.3.4\"\r\nframework: \"net9.0\"\r\n";

    /// <summary>
    /// The four tests above read the manifest through <see cref="FieldIn"/>,
    /// and this is the case that made every one of them fail on a Windows
    /// checkout while passing on the runner. The read is held to giving the
    /// same answer for both line endings, so the suite's verdict does not
    /// depend on the reader's `core.autocrlf`.
    /// </summary>
    [Fact]
    public void AFieldReadsTheSameWhicheverLineEndingTheManifestHas()
    {
        Assert.Equal("1.2.3.4", FieldIn(CrlfManifest, "version").Value);
        Assert.Equal(FieldIn(LfManifest, "version").Value, FieldIn(CrlfManifest, "version").Value);
        Assert.Null(FieldIn(CrlfManifest, "version").Failure);
    }

    /// <summary>
    /// A manifest that parses and simply has no such field, and a text that is
    /// not the manifest at all, are different failures. They were one message
    /// before this, so a read that could not see the file reported the file as
    /// incomplete, and the failure pointed away from its own cause.
    /// </summary>
    [Fact]
    public void AMissingFieldAndAnUnreadableManifestAreDifferentFailures()
    {
        var missing = FieldIn(LfManifest, "owner").Failure;
        var unreadable = FieldIn(string.Empty, "owner").Failure;

        Assert.NotNull(missing);
        Assert.Contains("declares no quoted 'owner' field", missing, StringComparison.Ordinal);

        Assert.NotNull(unreadable);
        Assert.DoesNotContain("declares no quoted", unreadable, StringComparison.Ordinal);
        Assert.Contains("no field at all", unreadable, StringComparison.Ordinal);
    }

    private static string ManifestField(string name)
    {
        var manifest = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "build.yaml"));
        var read = FieldIn(manifest, name);

        Assert.True(read.Failure is null, read.Failure);
        return read.Value!;
    }

    /// <summary>
    /// Reads one quoted scalar field out of the manifest text. The text is
    /// split into lines rather than matched whole under
    /// <see cref="RegexOptions.Multiline"/>, because in .NET a multiline `$`
    /// matches the position before a `\n` and leaves a `\r` sitting in front
    /// of it, so a trailing anchor never matches on a CRLF file. Splitting
    /// makes the carriage return part of the line, where it is trimmed once
    /// and by name.
    /// </summary>
    /// <param name="manifest">The manifest text, with either line ending.</param>
    /// <param name="name">The field to read.</param>
    /// <returns>The value, or the reason it could not be read.</returns>
    private static (string? Value, string? Failure) FieldIn(string manifest, string name)
    {
        var lines = manifest.Split('\n').ToList();
        var field = new Regex("^" + Regex.Escape(name) + ":[ \t]*\"([^\"]*)\"[ \t]*$");

        var match = lines.Select(l => field.Match(l)).FirstOrDefault(m => m.Success);
        if (match is not null)
        {
            return (match.Groups[1].Value, null);
        }

        // A text carrying no key line at all is not a manifest missing one
        // field. It is a file that was not read, was not the manifest, or was
        // not parsed, and saying so is what keeps the next reader from
        // editing build.yaml to add a field that is already there.
        if (!lines.Any(l => Regex.IsMatch(l, "^[A-Za-z][A-Za-z0-9_-]*:")))
        {
            return (null, "build.yaml parsed as no field at all, so it was not read as the manifest. Looking for '" + name + "'.");
        }

        return (null, "build.yaml declares no quoted '" + name + "' field.");
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
