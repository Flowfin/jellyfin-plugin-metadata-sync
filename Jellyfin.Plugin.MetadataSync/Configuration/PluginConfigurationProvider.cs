using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// Reads the configuration through a delegate supplied at registration, and
/// validates it before handing it over.
/// </summary>
/// <remarks>
/// The delegates are what keep the static and the server out of this type. In
/// the server one closes over the plugin instance and the other over the
/// library; in the suite they close over values the test made, and no global is
/// touched either way.
/// <para>
/// Nothing is kept between calls, and that is the property rather than an
/// omission. An operator repairs a configuration while the server runs, and a
/// verdict cached at the first read would go on refusing every action until a
/// restart. The cost is that both members read the library each time they are
/// asked, which is why neither of them is on a pass.
/// </para>
/// </remarks>
public sealed class PluginConfigurationProvider : IPluginConfigurationProvider
{
    private readonly Func<PluginConfiguration> _read;
    private readonly Func<IReadOnlyCollection<Guid>> _readLibrariesTheServerHolds;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfigurationProvider"/> class.
    /// </summary>
    /// <param name="read">Reads the current configuration.</param>
    /// <param name="readLibrariesTheServerHolds">
    /// Reads the libraries this server holds, which is the range a
    /// participating library is checked against.
    /// </param>
    public PluginConfigurationProvider(
        Func<PluginConfiguration> read,
        Func<IReadOnlyCollection<Guid>> readLibrariesTheServerHolds)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(readLibrariesTheServerHolds);
        _read = read;
        _readLibrariesTheServerHolds = readLibrariesTheServerHolds;
    }

    /// <inheritdoc />
    public PluginConfiguration Require()
    {
        var configuration = _read();
        var problems = ConfigurationValidation.Validate(configuration, _readLibrariesTheServerHolds());

        if (problems.Count > 0)
        {
            throw new ConfigurationRefusedException(problems);
        }

        return configuration;
    }

    /// <inheritdoc />
    public IReadOnlyList<ConfigurationProblem> Problems()
    {
        return ConfigurationValidation.Validate(_read(), _readLibrariesTheServerHolds());
    }
}
