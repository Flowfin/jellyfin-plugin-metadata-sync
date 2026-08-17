using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The suite compiles against the plugin under test and against nothing else
/// built from source, which is the half of #23 that does not wait on a double.
/// </summary>
/// <remarks>
/// #23 makes the pairing plugin's shipped test double the only counterparty this
/// suite ever talks to, and asks for an assertion that the real implementation is
/// not referenced by the test project. One route to that is a package, and
/// <see cref="HeadlessPolicyTests"/> already refuses a package outside a named
/// set. The other route is a project reference, which that guard does not read
/// at all: a sibling checkout added to the solution and referenced from here
/// arrives with no package name to refuse.
/// <para>
/// So this reads the project's own references and refuses a second one. It is
/// written before the double exists on purpose. Written now it refuses the first
/// line that breaks it; written afterwards it is a rule added on top of a call
/// site already in the tree, which is the shape that gets weakened rather than
/// obeyed.
/// </para>
/// <para>
/// The bound is worth having in writing. This reads what the project declares
/// and nothing else. A copy of somebody else's implementation pasted into this
/// repository as source declares no reference and is invisible here, and so is a
/// binary dropped beside the test host. What it catches is the route a second
/// implementation actually arrives by, which is a reference somebody added.
/// </para>
/// </remarks>
public class SuiteCounterpartyTests
{
    /// <summary>
    /// The one project the suite is allowed to be built against, named by its
    /// file rather than by the path that reaches it, so moving either project
    /// does not turn this into a rule about directory layout.
    /// </summary>
    private const string PluginUnderTest = "Jellyfin.Plugin.MetadataSync.csproj";

    /// <summary>
    /// A second implementation of anything the suite talks to has to be reachable
    /// before a test can talk to it, and a project reference is how one arrives
    /// without a package name for the headless allowed set to refuse.
    /// </summary>
    [Fact]
    public void TheTestProjectIsBuiltAgainstThePluginUnderTestAndNothingElse()
    {
        var referenced = ProjectReferencesIn(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Jellyfin.Plugin.MetadataSync.Tests.csproj")));

        Assert.Equal(new[] { PluginUnderTest }, referenced);
    }

    /// <summary>
    /// The refusing direction. A sibling checkout added to the solution and
    /// referenced from here is the way a real implementation reaches this suite
    /// without any package being named.
    /// </summary>
    [Fact]
    public void ASecondProjectBesideThePluginIsRefused()
    {
        var referenced = ProjectReferencesIn(
            Project(
                @"..\Jellyfin.Plugin.MetadataSync\Jellyfin.Plugin.MetadataSync.csproj",
                @"..\..\jellyfin-plugin-server-pairing\Jellyfin.Plugin.ServerPairing\Jellyfin.Plugin.ServerPairing.csproj"));

        Assert.NotEqual(new[] { PluginUnderTest }, referenced);
        Assert.Contains("Jellyfin.Plugin.ServerPairing.csproj", referenced, StringComparer.Ordinal);
    }

    /// <summary>
    /// The failing-open direction, and the reason the reader normalises rather
    /// than comparing the declared string. A reader understanding one separator
    /// finds no reference in a project written with the other, and a set with
    /// nothing in it is the state that reads exactly like a clean one.
    /// </summary>
    [Fact]
    public void AReferenceWrittenWithTheOtherSeparatorIsStillRead()
    {
        var referenced = ProjectReferencesIn(
            Project("../Jellyfin.Plugin.MetadataSync/Jellyfin.Plugin.MetadataSync.csproj"));

        Assert.Equal(new[] { PluginUnderTest }, referenced);
    }

    /// <summary>
    /// A project declaring no reference at all is refused rather than passing for
    /// having nothing wrong with it, which is what a guard asserting only that
    /// nothing forbidden appears would do.
    /// </summary>
    [Fact]
    public void AProjectThatReferencesNothingIsNotACleanOne()
    {
        var referenced = ProjectReferencesIn(Project());

        Assert.Empty(referenced);
        Assert.NotEqual(new[] { PluginUnderTest }, referenced);
    }

    /// <summary>
    /// Returns the file name of every project the given project file references,
    /// in a stable order, with either separator read.
    /// </summary>
    /// <param name="projectText">The project file's text.</param>
    /// <returns>The referenced project files, by name.</returns>
    private static IReadOnlyList<string> ProjectReferencesIn(string projectText)
    {
        return XDocument.Parse(projectText)
            .Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Replace('\\', '/'))
            .Select(v => v[(v.LastIndexOf('/') + 1)..])
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static string Project(params string[] references)
    {
        var items = string.Concat(
            references.Select(r => $"<ProjectReference Include=\"{r}\" />"));

        return $"<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>{items}</ItemGroup></Project>";
    }
}
