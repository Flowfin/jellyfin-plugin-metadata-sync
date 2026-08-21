using System;
using System.Collections.Generic;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Every service this plugin owns is built here with substituted dependencies
/// and no server present. A service that cannot be built this way is a service
/// that reaches a global, which is the thing these tests exist to stop.
/// </summary>
public class ServiceRegistrationTests
{
    private static readonly Guid _library = new("55555555-5555-5555-5555-555555555555");

    /// <summary>
    /// The configuration accessor returns what it was given, not what a static
    /// holds.
    /// </summary>
    [Fact]
    public void ConfigurationProviderReturnsTheConfigurationItWasGiven()
    {
        var supplied = new PluginConfiguration();

        IPluginConfigurationProvider provider = ProviderOver(supplied);

        Assert.Same(supplied, provider.Require());
    }

    /// <summary>
    /// Two providers built side by side see their own configuration. This is
    /// the property a static instance cannot have, and it is why two tests that
    /// pass alone stop failing together.
    /// </summary>
    [Fact]
    public void TwoProvidersDoNotShareAConfiguration()
    {
        var first = new PluginConfiguration();
        var second = new PluginConfiguration();

        IPluginConfigurationProvider one = ProviderOver(first);
        IPluginConfigurationProvider two = ProviderOver(second);

        Assert.Same(first, one.Require());
        Assert.Same(second, two.Require());
        Assert.NotSame(one.Require(), two.Require());
    }

    /// <summary>
    /// The provider refuses a missing delegate at construction rather than at
    /// first read, so a registration mistake surfaces where it was made.
    /// </summary>
    [Fact]
    public void ConfigurationProviderRefusesAMissingReader()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PluginConfigurationProvider(null!, () => Array.Empty<Guid>()));
    }

    /// <summary>
    /// The same, for the second delegate. A provider built without it would
    /// have nothing to check a participating library against, and the range it
    /// would fall back to is the empty one, which refuses every library an
    /// operator chose.
    /// </summary>
    [Fact]
    public void ConfigurationProviderRefusesAMissingLibraryReader()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PluginConfigurationProvider(() => new PluginConfiguration(), null!));
    }

    /// <summary>
    /// The registrator puts the configuration accessor in the container. It is
    /// called with no application host, because it must not need one.
    /// </summary>
    [Fact]
    public void RegistratorRegistersTheConfigurationAccessor()
    {
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, null!);

        var descriptor = Assert.Single(
            services,
            s => s.ServiceType == typeof(IPluginConfigurationProvider));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    private static PluginConfigurationProvider ProviderOver(PluginConfiguration configuration)
    {
        return new PluginConfigurationProvider(
            () => configuration,
            () => new List<Guid> { _library });
    }
}
