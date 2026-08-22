using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
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
/// A project reference is the route a second implementation usually arrives by
/// and it is not the only one. A copy of somebody else's implementation pasted
/// into this repository as source declares no reference, and a binary dropped
/// beside the test host declares nothing at all, so the second rule here reads
/// the built output instead of the project: every assembly beside the test host
/// is opened and asked which namespaces it declares, and one under
/// <c>Jellyfin.Plugin.</c> that is not this plugin's is a second plugin in the
/// directory the suite loads from.
/// </para>
/// <para>
/// That rule is derived from what an assembly declares rather than from what it
/// is called, which is what makes it worth reading the metadata. A pasted copy
/// arrives inside an assembly this repository already builds, so no file name
/// gives it away, and a dropped binary is named by whoever dropped it.
/// </para>
/// <para>
/// The bound is worth having in writing. The namespace root is what is read, so
/// a copy renamed into this plugin's own namespace is invisible, and so is one
/// whose types are under a name that is not <c>Jellyfin.Plugin.</c> at all. It
/// reads the directory as it stands when the suite runs, so a binary dropped
/// afterwards is outside it. And neither rule here says anything about what the
/// suite then talks to: that a test uses the double rather than a real
/// counterparty is the half of #23 that waits on the double existing.
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
    /// The namespace this plugin's own types are under. Everything this
    /// repository builds sits below it, so it is the one branch of
    /// <c>Jellyfin.Plugin.</c> that may appear beside the test host.
    /// </summary>
    private const string ThisPlugin = "Jellyfin.Plugin.MetadataSync";

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

    /// <summary>
    /// Whether a declared namespace belongs to a plugin that is not this one.
    /// </summary>
    /// <param name="declared">The namespace, as an assembly declares it.</param>
    /// <returns><c>true</c> if it is another plugin's.</returns>
    /// <remarks>
    /// The comparison is by segment and not by prefix. A namespace beginning
    /// with this plugin's name and carrying on without a separator is a
    /// different plugin whose name happens to start the same way, and a prefix
    /// test admits it while reading exactly like a working rule.
    /// </remarks>
    private static bool IsAnotherPluginsNamespace(string declared)
    {
        if (!declared.StartsWith("Jellyfin.Plugin.", StringComparison.Ordinal))
        {
            return false;
        }

        return !string.Equals(declared, ThisPlugin, StringComparison.Ordinal)
            && !declared.StartsWith(ThisPlugin + ".", StringComparison.Ordinal);
    }

    /// <summary>
    /// Every namespace declared by an assembly sitting beside the test host.
    /// </summary>
    /// <returns>The namespaces, in a stable order and without repeats.</returns>
    /// <remarks>
    /// A file carrying no metadata is native and declares no namespace, which is
    /// the only thing passed over. A file that is not an image at all is left to
    /// throw rather than be swallowed, because a reader that quietly drops what
    /// it cannot parse is a reader that reports a clean directory.
    /// </remarks>
    private static IReadOnlyList<string> NamespacesBesideTheTestHost()
    {
        var declared = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            using var stream = File.OpenRead(file);
            using var portableExecutable = new PEReader(stream);

            if (!portableExecutable.HasMetadata)
            {
                continue;
            }

            var metadata = portableExecutable.GetMetadataReader();

            foreach (var handle in metadata.TypeDefinitions)
            {
                var name = metadata.GetString(metadata.GetTypeDefinition(handle).Namespace);

                if (name.Length != 0)
                {
                    declared.Add(name);
                }
            }
        }

        return declared.ToList();
    }

    /// <summary>
    /// The rule the project reference cannot reach. Nothing in the directory the
    /// suite loads from is a second plugin, however it arrived there.
    /// </summary>
    [Fact]
    public void NoAssemblyBesideTheTestHostBelongsToASecondPlugin()
    {
        var foreign = NamespacesBesideTheTestHost()
            .Where(IsAnotherPluginsNamespace)
            .ToList();

        Assert.Empty(foreign);
    }

    /// <summary>
    /// The failing-open direction, and the reason this leg exists at all. A
    /// reader that opened nothing, or that read every assembly and took no
    /// namespace out of it, answers with an empty set, and an empty set is what
    /// a clean directory answers with too. So the reading is asserted to have
    /// found this plugin's own types before its silence is trusted.
    /// </summary>
    [Fact]
    public void TheReadingFindsThisPluginsOwnNamespaces()
    {
        var declared = NamespacesBesideTheTestHost();

        Assert.Contains(ThisPlugin, declared, StringComparer.Ordinal);
        Assert.Contains(ThisPlugin + ".Fields", declared, StringComparer.Ordinal);
        Assert.Contains(ThisPlugin + ".Tests", declared, StringComparer.Ordinal);
    }

    /// <summary>
    /// The refusing direction, over the namespaces a second implementation
    /// actually arrives under: the pairing plugin this suite is meant to reach
    /// only through a double, and any other plugin in the same family.
    /// </summary>
    /// <param name="declared">A namespace another plugin would declare.</param>
    [Theory]
    [InlineData("Jellyfin.Plugin.ServerPairing")]
    [InlineData("Jellyfin.Plugin.ServerPairing.Protocol")]
    [InlineData("Jellyfin.Plugin.ServerPairing.Matching")]
    [InlineData("Jellyfin.Plugin.Sso")]
    public void ASecondPluginsNamespaceIsRefused(string declared)
    {
        Assert.True(IsAnotherPluginsNamespace(declared));
    }

    /// <summary>
    /// The near miss. Drop the separator and the name is a different plugin
    /// whose own name begins the same way, which a prefix comparison admits.
    /// </summary>
    /// <param name="declared">A namespace that only begins like this plugin's.</param>
    [Theory]
    [InlineData("Jellyfin.Plugin.MetadataSyncExtra")]
    [InlineData("Jellyfin.Plugin.MetadataSyncer.Protocol")]
    public void ANamespaceThatOnlyBeginsLikeThisPluginIsRefused(string declared)
    {
        Assert.True(IsAnotherPluginsNamespace(declared));
    }

    /// <summary>
    /// What the rule must not refuse. This plugin's own namespaces, and the
    /// server's, which are not plugins at all and share the first segment.
    /// </summary>
    /// <param name="declared">A namespace that belongs in this directory.</param>
    [Theory]
    [InlineData("Jellyfin.Plugin.MetadataSync")]
    [InlineData("Jellyfin.Plugin.MetadataSync.Fields")]
    [InlineData("Jellyfin.Plugin.MetadataSync.Tests")]
    [InlineData("Jellyfin.Data.Enums")]
    [InlineData("Jellyfin.Extensions")]
    [InlineData("MediaBrowser.Controller.Entities")]
    public void ANamespaceThatBelongsHereIsNotRefused(string declared)
    {
        Assert.False(IsAnotherPluginsNamespace(declared));
    }
}
