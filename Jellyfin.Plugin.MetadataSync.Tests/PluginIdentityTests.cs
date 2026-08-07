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
    [Fact]
    public void ConfigurationPageResourceResolvesFromThePluginNamespace()
    {
        var expected = $"{typeof(Plugin).Namespace}.Configuration.configPage.html";

        Assert.Contains(expected, PluginAssembly.GetManifestResourceNames());
    }

    private static string ManifestPath() => Path.Combine(AppContext.BaseDirectory, "build.yaml");
}
