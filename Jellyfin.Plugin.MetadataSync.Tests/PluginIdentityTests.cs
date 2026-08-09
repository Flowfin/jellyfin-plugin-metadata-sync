using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Guards the plugin identity against the two ways it silently breaks: an
/// assembly the manifest does not name, and a configuration page the server
/// cannot find.
/// </summary>
public class PluginIdentityTests
{
    private static readonly Assembly PluginAssembly = typeof(Plugin).Assembly;

    /// <summary>
    /// The manifest lists the artefact by file name. A rename that misses the
    /// manifest produces a package the server unpacks and then cannot load,
    /// which looks like an install that worked.
    /// </summary>
    [Fact]
    public void ManifestNamesTheAssemblyTheBuildProduces()
    {
        var assemblyName = PluginAssembly.GetName().Name;
        Assert.NotNull(assemblyName);

        var manifest = File.ReadAllText(ManifestPath());

        Assert.Contains($"- \"{assemblyName}.dll\"", manifest, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page path is built from the plugin type's namespace at run time, so
    /// a namespace that no longer matches the embedded resource name gives an
    /// operator a configuration page that fails to load, with nothing said at
    /// build time.
    /// </summary>
    /// <remarks>
    /// The path is taken from the declaration the server reads rather than
    /// rebuilt here from the same namespace. Rebuilt, this passed on any change
    /// that moved the declaration, because the copy in the test moved with the
    /// namespace and never with the declaration.
    /// </remarks>
    [Fact]
    public void ConfigurationPageResourceResolvesFromThePluginNamespace()
    {
        Assert.Contains(DeclaredPages.ConfigurationPagePath(), PluginAssembly.GetManifestResourceNames());
    }

    /// <summary>
    /// One page, and the tests that read it read that one. Two declarations
    /// would leave every leg here asserting about whichever came first.
    /// </summary>
    [Fact]
    public void ThePluginDeclaresOnePage()
    {
        Assert.Single(DeclaredPages.All());
    }

    private static string ManifestPath() => Path.Combine(AppContext.BaseDirectory, "build.yaml");
}
