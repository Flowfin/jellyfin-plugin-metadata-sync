using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Reads the plugin project file, which is the declaration of what the build
/// produces and against which server packages.
/// </summary>
/// <remarks>
/// The project targets one framework per supported server line and references a
/// different pair of server packages under each, so a reading that asks for
/// "the" target framework or "the" package version answers about whichever one
/// the file happens to list first. Every reader here takes the line it is
/// asking about.
/// <para>
/// It is one type rather than a copy in each suite that needs it. Two readings
/// of one file drift, and the copy a failing test happens to use is then the
/// one that is wrong.
/// </para>
/// </remarks>
internal static class PluginProjectFile
{
    /// <summary>
    /// Gets the target frameworks the plugin project builds, in the order it
    /// declares them.
    /// </summary>
    /// <returns>The target framework monikers.</returns>
    public static IReadOnlyList<string> TargetFrameworks()
    {
        var declared = Document().Descendants("TargetFrameworks").Select(e => e.Value.Trim()).FirstOrDefault();

        Assert.False(
            string.IsNullOrEmpty(declared),
            "The plugin project declares no <TargetFrameworks>. One framework per supported server line is what makes the second line something the build reads rather than something a document claims.");

        return declared!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Gets the version of a server package the plugin project references for
    /// one target framework.
    /// </summary>
    /// <param name="packageId">The package.</param>
    /// <param name="targetFramework">The target framework whose reference is wanted.</param>
    /// <returns>The declared version.</returns>
    public static string PackageVersion(string packageId, string targetFramework)
    {
        var declared = Document().Descendants("PackageReference")
            .Where(e => string.Equals(e.Attribute("Include")?.Value, packageId, StringComparison.Ordinal))
            .Where(e => AppliesTo(e, targetFramework))
            .Select(e => e.Attribute("Version")?.Value)
            .FirstOrDefault();

        Assert.False(
            string.IsNullOrEmpty(declared),
            "The plugin project references no " + packageId + " with a version for " + targetFramework + ".");

        return declared!;
    }

    /// <summary>
    /// Whether a reference applies when the project is built for one target
    /// framework.
    /// </summary>
    /// <remarks>
    /// A reference under no condition applies to every framework. A reference
    /// under a condition applies to the frameworks that condition names, and the
    /// condition is read by looking for the moniker inside it rather than by
    /// evaluating MSBuild: the shape used here is one equality against
    /// <c>$(TargetFramework)</c>, and a condition this reading cannot place is
    /// refused rather than guessed at.
    /// </remarks>
    /// <param name="reference">The reference element.</param>
    /// <param name="targetFramework">The target framework being asked about.</param>
    /// <returns>Whether the reference applies.</returns>
    private static bool AppliesTo(XElement reference, string targetFramework)
    {
        var condition = reference.Ancestors()
            .Select(a => a.Attribute("Condition")?.Value)
            .FirstOrDefault(c => !string.IsNullOrEmpty(c));

        if (string.IsNullOrEmpty(condition))
        {
            return true;
        }

        Assert.Contains("$(TargetFramework)", condition, StringComparison.Ordinal);

        var named = TargetFrameworks().Where(tfm => condition.Contains("'" + tfm + "'", StringComparison.Ordinal)).ToList();

        Assert.True(
            named.Count > 0,
            "A package reference in the plugin project is under the condition " + condition
                + ", which names none of the target frameworks the project declares.");

        return named.Contains(targetFramework, StringComparer.Ordinal);
    }

    /// <summary>
    /// Opens the copy of the project file the test project puts beside the test
    /// binary.
    /// </summary>
    /// <remarks>
    /// Joined rather than combined. <see cref="Path.Combine(string, string)"/>
    /// returns its second argument whole when that argument is rooted, so a
    /// reader of it has to know the name is a constant before they know which
    /// file is opened. The code analysis says the same thing as
    /// <c>cs/path-combine</c>, and satisfying it by writing what was meant is
    /// cheaper than an alert somebody has to dismiss again next time.
    /// </remarks>
    /// <returns>The project file.</returns>
    private static XDocument Document()
        => XDocument.Load(Path.Join(AppContext.BaseDirectory, "Jellyfin.Plugin.MetadataSync.csproj"));
}
