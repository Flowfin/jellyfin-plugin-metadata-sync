namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// Supplies this plugin's configuration to the services that need it.
/// </summary>
/// <remarks>
/// Exists so that nothing but the plugin entry point has to know the
/// configuration comes from a static. A service that reads
/// <c>Plugin.Instance.Configuration</c> cannot be handed a different
/// configuration in a test, which is how a suite ends up mutating a global
/// between tests and how two tests that pass alone fail together.
/// </remarks>
public interface IPluginConfigurationProvider
{
    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    PluginConfiguration Configuration { get; }
}
