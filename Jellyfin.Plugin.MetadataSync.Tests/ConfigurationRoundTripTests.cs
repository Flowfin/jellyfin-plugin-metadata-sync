using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using Jellyfin.Plugin.MetadataSync.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The configuration survives being written to disk and read back.
/// </summary>
/// <remarks>
/// This is the test that catches a property added later in a shape the
/// serialiser cannot carry. That failure is silent in the worst way: the
/// property is set on the page, the save reports success, and the value is gone
/// the next time the server starts. An operator who excluded a field would find
/// it moving again after a restart and no message anywhere saying why.
/// <para>
/// The serialisation is XML because that is what the server hands this plugin.
/// The base class takes an <see cref="IXmlSerializer"/> and nothing else, which
/// is asserted below rather than assumed, so the choice of serialiser here is
/// derived from the server's surface instead of from habit.
/// </para>
/// <para>
/// What this does not do. The server's own implementation of that interface is
/// in neither package this tree references, so what runs here is
/// <see cref="XmlSerializer"/> rather than the instance the server would pass
/// in. A shape refused by that class is refused by any XML serialiser; a shape
/// this passes is one this plugin has not seen the server's own writer handle.
/// </para>
/// </remarks>
public class ConfigurationRoundTripTests
{
    /// <summary>
    /// Why XML. The server constructs this plugin with a serialiser, and this
    /// asserts which kind, so the round trip below is through the format the
    /// configuration is actually stored in.
    /// </summary>
    [Fact]
    public void TheServerStoresThisConfigurationAsXml()
    {
        var parameters = typeof(Plugin)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToList();

        Assert.Contains(typeof(IXmlSerializer), parameters);
        Assert.Equal(typeof(BasePlugin<PluginConfiguration>), typeof(Plugin).BaseType);
    }

    /// <summary>
    /// Everything an operator can express, written out and read back, compared
    /// property by property.
    /// </summary>
    /// <remarks>
    /// The comparison is derived from the type rather than written as a list of
    /// assertions. A property added to the configuration is compared here
    /// without anybody remembering to add a line, which is the direction that
    /// fails closed.
    /// </remarks>
    [Fact]
    public void NothingAnOperatorSetIsLostOnTheWayToDiskAndBack()
    {
        var before = Everything();

        Assert.Empty(WhatDiffers(before, RoundTrip(before)));
    }

    /// <summary>
    /// The bite. A comparison that always answers "nothing differs" would pass
    /// the test above on a serialiser that dropped every value, so the
    /// comparison is shown noticing a difference in each property in turn.
    /// </summary>
    /// <param name="property">The property to change before comparing.</param>
    [Theory]
    [InlineData(nameof(PluginConfiguration.PairingId))]
    [InlineData(nameof(PluginConfiguration.Direction))]
    [InlineData(nameof(PluginConfiguration.ParticipatingLibraries))]
    [InlineData(nameof(PluginConfiguration.ExcludedFields))]
    [InlineData(nameof(PluginConfiguration.Format))]
    public void TheComparisonNoticesAValueThatDidNotSurvive(string property)
    {
        var before = Everything();
        var damaged = Everything();

        switch (property)
        {
            case nameof(PluginConfiguration.PairingId):
                damaged.PairingId = Guid.Empty;
                break;
            case nameof(PluginConfiguration.Direction):
                damaged.Direction = (SyncDirection)7;
                break;
            case nameof(PluginConfiguration.ParticipatingLibraries):
                damaged.ParticipatingLibraries.Clear();
                break;
            case nameof(PluginConfiguration.ExcludedFields):
                damaged.ExcludedFields.Clear();
                break;
            case nameof(PluginConfiguration.Format):
                damaged.Format = ConfigurationFormat.Absent;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, "No damage is declared for this property.");
        }

        Assert.Equal(new[] { property }, WhatDiffers(before, damaged));
    }

    /// <summary>
    /// The fixture has to set every property to something that is not its
    /// default, or the round trip proves nothing about the ones it left alone:
    /// a value the serialiser dropped and a value that was never set read back
    /// identically.
    /// </summary>
    /// <remarks>
    /// One property cannot satisfy that and is exempted by what it is rather
    /// than by name. <see cref="SyncDirection"/> declares one member, so it has
    /// no value other than its default to set, and the round trip is silent
    /// about it. That is the bound rather than a hole somebody forgot: the day
    /// a second direction is declared, the exemption stops applying and this
    /// fails until the fixture sets it.
    /// </remarks>
    [Fact]
    public void TheFixtureSetsEveryPropertyThatHasMoreThanOneValueToSet()
    {
        var untouched = new PluginConfiguration();

        var atTheirDefaults = Declared()
            .Where(property => !HasOnlyOneValue(property.PropertyType))
            .Where(property => !Differs(property.GetValue(Everything()), property.GetValue(untouched)))
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(atTheirDefaults);
    }

    /// <summary>
    /// A type an operator has no second value to choose. Today that is an enum
    /// with one member, and nothing else in the configuration is one.
    /// </summary>
    private static bool HasOnlyOneValue(Type type)
    {
        return type.IsEnum && Enum.GetValues(type).Length == 1;
    }

    /// <summary>
    /// A configuration with every property set to something that is not its
    /// default.
    /// </summary>
    private static PluginConfiguration Everything()
    {
        var configuration = new PluginConfiguration
        {
            PairingId = Guid.Parse("5c37c448-9d94-4fdd-a621-4238b859165b"),
            Direction = SyncDirection.TwoWay,

            // Not the default, which is the absent stamp. A round trip over the
            // value the serialiser would have left anyway proves nothing about
            // whether this property survives the journey.
            Format = ConfigurationFormat.Current,
        };

        configuration.ParticipatingLibraries.Add(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        configuration.ParticipatingLibraries.Add(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        configuration.ExcludedFields.Add("Overview");
        configuration.ExcludedFields.Add("Tagline");

        return configuration;
    }

    private static PluginConfiguration RoundTrip(PluginConfiguration configuration)
    {
        var serialiser = new XmlSerializer(typeof(PluginConfiguration));

        using var written = new MemoryStream();
        serialiser.Serialize(written, configuration);

        written.Position = 0;

        // The reader is configured rather than defaulted. A plugin
        // configuration is a file on the server's disk, and a document type
        // definition in it is either an operator's mistake or somebody else's
        // idea, neither of which this has any reason to process.
        using var reader = XmlReader.Create(written, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var read = serialiser.Deserialize(reader);

        Assert.NotNull(read);
        return (PluginConfiguration)read;
    }

    private static IReadOnlyList<string> WhatDiffers(PluginConfiguration before, PluginConfiguration after)
    {
        return Declared()
            .Where(property => Differs(property.GetValue(before), property.GetValue(after)))
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Compares two property values. A collection is compared by what it holds
    /// and in what order, because two collections that are not the same object
    /// are never equal to each other and the whole comparison would answer
    /// "everything differs" for the wrong reason.
    /// </summary>
    private static bool Differs(object? before, object? after)
    {
        if (before is IEnumerable left and not string && after is IEnumerable right and not string)
        {
            return !left.Cast<object>().SequenceEqual(right.Cast<object>());
        }

        return !Equals(before, after);
    }

    private static IReadOnlyList<PropertyInfo> Declared()
    {
        return typeof(PluginConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToList();
    }
}
