using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Keeps the plugin configuration to choices, and out of data.
/// </summary>
/// <remarks>
/// Plugin configuration is serialised to a file on disk with the server's own
/// permissions, read back by the server, handed to the plugin page, and pasted
/// into a support thread by an operator whose sync is not working. Anything that
/// travels that way had better be a decision somebody made and not a value
/// copied out of their library, a peer address or a token.
///
/// This plugin holds no key material at all, which #20 makes structural rather
/// than a rule anybody has to keep. What is left is everything else that is
/// quietly sensitive, and the guard for it is a shape assertion rather than a
/// review habit: a property added later fails the suite until somebody puts it
/// in the allowed set on purpose.
///
/// The second leg is the one that survives the plugin growing. Even a property
/// that belongs here must be a choice: a number, a flag, a name, a set of those.
/// A property whose type is a record, a store, a log entry or an item drags data
/// into the configuration file however innocent the property name looks, and it
/// is refused by what it is made of rather than by what it is called.
///
/// What it cannot catch, stated rather than left to be assumed. It reads the
/// configuration type, so data written to disk by some other route is outside
/// it, and so is a string property holding something sensitive: a peer address
/// is a string like any other and no reading of the type says which. That half
/// is #20's, which removes the address rather than classifies it.
/// </remarks>
public class ConfigurationShapeTests
{
    /// <summary>
    /// The properties the configuration is allowed to carry, and why each one
    /// is here. Five are a thing an operator decides that this plugin then acts
    /// on, and the sixth is a stamp saying which shape the file is written in.
    /// Every one of the six carries a validation rule that can refuse it.
    /// <list type="bullet">
    /// <item><description>
    /// PairingId. Which pairing this configuration is for. An identifier the
    /// operator picks from what the pairing plugin holds, not an address and
    /// not a credential.
    /// </description></item>
    /// <item><description>
    /// Direction. Which way metadata moves. One value belonging to the pairing,
    /// so it is a choice with one member rather than a column per field.
    /// </description></item>
    /// <item><description>
    /// ParticipatingLibraries. Which libraries take part, by the identifier the
    /// server already holds them under. Identifiers rather than names, because
    /// a name is a thing an operator renames.
    /// </description></item>
    /// <item><description>
    /// ExcludedFields. Which of the fields the register allows to move this
    /// operator does not want moved. Field names, which are this plugin's own
    /// vocabulary rather than anything out of a library.
    /// </description></item>
    /// <item><description>
    /// ItemsPerRead. How many items a pass asks the server for at once. A
    /// number describing this plugin's own behaviour on this server, which is
    /// nothing about anybody and safe in the support thread this document is
    /// about.
    /// </description></item>
    /// <item><description>
    /// Format. Which shape this file is written in. It is the one member here
    /// that nobody chooses, and admitting it is a widening of what this set is
    /// for rather than one more of the same kind, so the reason is written
    /// rather than assumed. What this guard is against is data - a value copied
    /// out of a library, a peer address, a credential - and a number naming this
    /// file's own shape is none of the three: it says nothing about anybody, it
    /// is safe in the support thread this document is about, and it is refused
    /// by the same validation as everything above. What it buys is that a
    /// configuration written by a newer build is refused instead of being acted
    /// on under rules it was not written under, which is #59.
    /// </description></item>
    /// </list>
    /// </summary>
    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        nameof(PluginConfiguration.PairingId),
        nameof(PluginConfiguration.Direction),
        nameof(PluginConfiguration.ParticipatingLibraries),
        nameof(PluginConfiguration.ExcludedFields),
        nameof(PluginConfiguration.ItemsPerRead),
        nameof(PluginConfiguration.Format),
    };

    /// <summary>
    /// A property of a shape that is a choice. Anything else is data.
    /// </summary>
    private static bool IsAChoice(Type type)
    {
        var bare = Nullable.GetUnderlyingType(type) ?? type;

        if (bare.IsPrimitive || bare.IsEnum)
        {
            return true;
        }

        if (bare == typeof(string) || bare == typeof(decimal) || bare == typeof(Guid)
            || bare == typeof(TimeSpan) || bare == typeof(Uri))
        {
            return true;
        }

        if (bare.IsArray)
        {
            var element = bare.GetElementType();
            return element is not null && IsAChoice(element);
        }

        if (bare.IsGenericType && typeof(IEnumerable).IsAssignableFrom(bare))
        {
            return bare.GetGenericArguments().All(IsAChoice);
        }

        return false;
    }

    /// <summary>
    /// The rule. A property on the configuration that nobody put in the allowed
    /// set fails, so adding one is a decision rather than a diff nobody reads.
    /// </summary>
    [Fact]
    public void TheConfigurationCarriesNoPropertyOutsideTheAllowedSet()
    {
        var outside = DeclaredProperties()
            .Select(p => p.Name)
            .Where(name => !AllowedProperties.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(outside);
    }

    /// <summary>
    /// The rule that survives the plugin growing. Every property the
    /// configuration carries, now and later, is made of the shapes a choice is
    /// made of. A store, a log entry, a record of what was written or a library
    /// item is refused by its type rather than by its name.
    /// </summary>
    [Fact]
    public void EveryConfigurationPropertyIsAChoiceRatherThanData()
    {
        var data = DeclaredProperties()
            .Where(p => !IsAChoice(p.PropertyType))
            .Select(p => p.Name + " is " + p.PropertyType.FullName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(data);
    }

    /// <summary>
    /// The scan reads a type. If the property reader ever answers for the wrong
    /// type it passes for the wrong reason, so what it reads is asserted rather
    /// than assumed.
    /// </summary>
    [Fact]
    public void TheScanReadsThePluginsOwnConfigurationType()
    {
        Assert.Equal("Jellyfin.Plugin.MetadataSync.Configuration.PluginConfiguration", typeof(PluginConfiguration).FullName);
        Assert.Contains(typeof(PluginConfiguration), typeof(Plugin).Assembly.GetTypes());
    }

    /// <summary>
    /// The bite for the second rule, run rather than argued. The shapes below
    /// are the ones a configuration acquires when somebody reaches for the
    /// nearest place to persist something.
    /// </summary>
    /// <param name="offered">A type a property might have.</param>
    [Theory]
    [InlineData(typeof(MediaBrowser.Controller.Entities.BaseItem))]
    [InlineData(typeof(Dictionary<string, MediaBrowser.Controller.Entities.BaseItem>))]
    [InlineData(typeof(MediaBrowser.Controller.Entities.BaseItem[]))]
    [InlineData(typeof(IPluginConfigurationProvider))]
    public void DataIsRefusedByItsShape(Type offered)
    {
        Assert.False(IsAChoice(offered));
    }

    /// <summary>
    /// The neighbour. These are what a real setting looks like, and a rule that
    /// refused them would refuse every configuration this plugin is going to
    /// need. A collection of strings sits one type argument away from the
    /// collection of items refused above.
    /// </summary>
    /// <param name="offered">A type a property might have.</param>
    [Theory]
    [InlineData(typeof(bool))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(string))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(string[]))]
    [InlineData(typeof(IReadOnlyList<Guid>))]
    [InlineData(typeof(Dictionary<string, bool>))]
    public void AChoiceIsAccepted(Type offered)
    {
        Assert.True(IsAChoice(offered));
    }

    /// <summary>
    /// Reads the properties declared on the plugin's own configuration type. The
    /// base class the server requires carries its own, and those are the
    /// server's decision rather than this plugin's.
    /// </summary>
    private static IReadOnlyList<PropertyInfo> DeclaredProperties()
    {
        return typeof(PluginConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToList();
    }
}
