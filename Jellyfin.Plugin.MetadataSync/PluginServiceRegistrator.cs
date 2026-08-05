using Jellyfin.Plugin.MetadataSync.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MetadataSync;

/// <summary>
/// Registers every service this plugin owns with the server's container.
/// </summary>
/// <remarks>
/// This type and <see cref="Plugin"/> are the only two places allowed to read
/// <see cref="Plugin.Instance"/>. Everything downstream is handed what it needs
/// through a constructor, so it can be built in a test with no server present.
/// </remarks>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IPluginConfigurationProvider>(
            _ => new PluginConfigurationProvider(() => Plugin.Instance!.Configuration));
    }
}
