using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
/// <remarks>
/// Deliberately empty. Every setting this plugin holds is decided by the
/// configuration design rather than carried over from the project this tree
/// was started from, and a property that ships before it is designed is a
/// surface an operator can set and the plugin never reads.
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
}
