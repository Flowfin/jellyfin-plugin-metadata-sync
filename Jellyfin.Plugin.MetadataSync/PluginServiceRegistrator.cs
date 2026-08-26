using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Store;
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

        // The format the store directory is written in. It is registered as a
        // store of its own because it persists, and it answers every pairing
        // with no rows: what it holds is not data about a relationship with
        // another server. It reads the stamp on every question rather than
        // keeping one, so this instance and the one the store reads through
        // cannot drift apart.
        serviceCollection.AddSingleton(
            _ => new StoreFormat(Plugin.Instance!.DataFolderPath));
        serviceCollection.AddSingleton<IPairingStore>(
            services => services.GetRequiredService<StoreFormat>());

        // One instance for the whole server. The store keeps what it holds in
        // memory and appends to one file, so a second instance over the same
        // directory would be a second answer to the same question with its own
        // copy of the file's tail. It is built lazily for the same reason the
        // library is resolved inside the delegate: nothing may touch a disk
        // while the container is still being assembled.
        //
        // The concrete type is what the container holds and the two interfaces
        // are forwarded to it, so both faces of the store are the same object.
        // Registering each interface with its own factory would have built two
        // stores over one file, and the one that reported what is held would
        // have been a different one from the one a pass wrote to.
        serviceCollection.AddSingleton(
            _ => new WrittenValues(Plugin.Instance!.DataFolderPath));
        serviceCollection.AddSingleton<IWrittenValues>(
            services => services.GetRequiredService<WrittenValues>());
        serviceCollection.AddSingleton<IPairingStore>(
            services => services.GetRequiredService<WrittenValues>());

        // What an operator asks what this plugin holds about one pairing, and
        // asks to have removed. It is given every store rather than a list
        // written here, so a store added later reaches the report by being
        // registered rather than by somebody remembering this line.
        serviceCollection.AddSingleton(
            services => new PairingStores(services.GetServices<IPairingStore>()));
    }
}
