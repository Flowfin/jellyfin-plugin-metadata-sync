using Jellyfin.Plugin.MetadataSync.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
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
/// <para>
/// The library is resolved inside the delegate rather than at registration.
/// Registration runs while the container is still being assembled, and the
/// range a configuration is checked against has to be the libraries the server
/// holds when somebody asks, not the ones it held at start-up.
/// </para>
/// </remarks>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IPluginConfigurationProvider>(
            services => new PluginConfigurationProvider(
                () => Plugin.Instance!.Configuration,
                () => ServerLibraries.Held(services.GetRequiredService<ILibraryManager>())));
    }
}
