using System;
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
    /// <summary>
    /// The configuration accessor returns what it was given, not what a static
    /// holds.
    /// </summary>
    [Fact]
    public void ConfigurationProviderReturnsTheConfigurationItWasGiven()
    {
        var supplied = new PluginConfiguration();

        IPluginConfigurationProvider provider = new PluginConfigurationProvider(() => supplied);

        Assert.Same(supplied, provider.Configuration);
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

        IPluginConfigurationProvider one = new PluginConfigurationProvider(() => first);
        IPluginConfigurationProvider two = new PluginConfigurationProvider(() => second);

        Assert.Same(first, one.Configuration);
        Assert.Same(second, two.Configuration);
        Assert.NotSame(one.Configuration, two.Configuration);
    }

    /// <summary>
    /// The provider refuses a missing delegate at construction rather than at
    /// first read, so a registration mistake surfaces where it was made.
    /// </summary>
    [Fact]
    public void ConfigurationProviderRefusesAMissingReader()
    {
        Assert.Throws<ArgumentNullException>(() => new PluginConfigurationProvider(null!));
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
}
