using System;

namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// Reads the configuration through a delegate supplied at registration.
/// </summary>
/// <remarks>
/// The delegate is what keeps the static out of this type. In the server it
/// closes over the plugin instance; in the suite it closes over a
/// configuration the test made, and no global is touched either way.
/// </remarks>
public sealed class PluginConfigurationProvider : IPluginConfigurationProvider
{
    private readonly Func<PluginConfiguration> _read;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfigurationProvider"/> class.
    /// </summary>
    /// <param name="read">Reads the current configuration.</param>
    public PluginConfigurationProvider(Func<PluginConfiguration> read)
    {
        _read = read;
    }

    /// <inheritdoc />
    public PluginConfiguration Configuration => _read();
}
