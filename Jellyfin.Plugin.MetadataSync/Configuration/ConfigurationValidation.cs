using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Fields;

namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// Reads a configuration and says what about it cannot be acted on.
/// </summary>
/// <remarks>
/// One reading, used twice. Saving is where an operator gets a sentence they
/// can do something about; loading is where a file edited by hand, or written
/// by an older version of this plugin, is caught before a pass runs on it. Two
/// validators would drift, and the one that drifts is always the one on the
/// load path, because nobody watches it.
/// <para>
/// It answers with problems rather than by throwing. A configuration that
/// fails on load has to be reported on the page beside every action it
/// disables, and an exception carries one sentence where an operator needs the
/// list. Refusing is what the caller then does with a list that is not empty,
/// and it is refusing all of them rather than the first one.
/// </para>
/// <para>
/// The libraries the server holds are handed in rather than read. This type
/// touches no library, no clock, no file and no network, so a test constructs
/// the whole check out of values with no substitutes of any kind.
/// </para>
/// </remarks>
public static class ConfigurationValidation
{
    /// <summary>
    /// Returns every reason this configuration cannot be acted on, in a fixed
    /// order. An empty answer means it can.
    /// </summary>
    /// <param name="configuration">The configuration, as saved or as read back.</param>
    /// <param name="librariesTheServerHolds">
    /// The libraries this server holds, by identifier. This is the range a
    /// participating library is checked against.
    /// </param>
    /// <returns>The problems, or an empty list.</returns>
    public static IReadOnlyList<ConfigurationProblem> Validate(
        PluginConfiguration configuration,
        IReadOnlyCollection<Guid> librariesTheServerHolds)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(librariesTheServerHolds);

        var problems = new List<ConfigurationProblem>();

        Format(configuration, problems);
        Direction(configuration, problems);
        ParticipatingLibraries(configuration, librariesTheServerHolds, problems);
        ExcludedFields(configuration, problems);
        PairingId(configuration, problems);

        return problems;
    }

    /// <summary>
    /// The range is the formats this build reads, plus the absent stamp every
    /// configuration in existence carries today.
    /// </summary>
    /// <remarks>
    /// This is checked first so a file this build cannot place is the first
    /// thing said about it. Every other rule below reads a property under the
    /// meaning the current shape gives it, so a list that led with those would
    /// be a list of judgements made against the wrong shape.
    /// <para>
    /// It is a problem rather than a throw, which is the same answer the rest of
    /// this type gives and matters more here than elsewhere: a configuration
    /// written by a newer build is exactly the case an operator has to be told
    /// about on a page, and a refusal that threw would disable the page's own
    /// reading along with everything else.
    /// </para>
    /// </remarks>
    private static void Format(PluginConfiguration configuration, List<ConfigurationProblem> problems)
    {
        if (ConfigurationFormat.Declared(configuration.Format) is not null)
        {
            return;
        }

        problems.Add(new ConfigurationProblem(
            nameof(PluginConfiguration.Format),
            configuration.Format > ConfigurationFormat.Current
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "Format says '{0}', which is a configuration shape newer than this build reads ('{1}').",
                    configuration.Format,
                    ConfigurationFormat.Current)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "Format says '{0}', which is no configuration shape this build can read.",
                    configuration.Format)));
    }

    /// <summary>
    /// The range is the members this plugin declares. A configuration read off
    /// disk can carry any number the underlying type holds, so this is a check
    /// over a value rather than over an operator's choice.
    /// </summary>
    private static void Direction(PluginConfiguration configuration, List<ConfigurationProblem> problems)
    {
        if (!Enum.IsDefined(configuration.Direction))
        {
            problems.Add(new ConfigurationProblem(
                nameof(PluginConfiguration.Direction),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Direction carries the value '{0}', which is not a direction this plugin declares.",
                    (int)configuration.Direction)));
        }
    }

    /// <summary>
    /// The range is any subset of the libraries the server holds. The empty set
    /// is inside it and means no library takes part.
    /// </summary>
    /// <remarks>
    /// A library the server no longer holds is the case this is written for. It
    /// happens without anybody editing the configuration: an operator removes a
    /// library and the identifier stays behind, and a pass that skipped it
    /// silently would sync less than the page says it does.
    /// </remarks>
    private static void ParticipatingLibraries(
        PluginConfiguration configuration,
        IReadOnlyCollection<Guid> librariesTheServerHolds,
        List<ConfigurationProblem> problems)
    {
        var seen = new HashSet<Guid>();

        foreach (var library in configuration.ParticipatingLibraries)
        {
            if (!seen.Add(library))
            {
                problems.Add(new ConfigurationProblem(
                    nameof(PluginConfiguration.ParticipatingLibraries),
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ParticipatingLibraries names the library '{0}' more than once.",
                        library)));
                continue;
            }

            if (!librariesTheServerHolds.Contains(library))
            {
                problems.Add(new ConfigurationProblem(
                    nameof(PluginConfiguration.ParticipatingLibraries),
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ParticipatingLibraries names the library '{0}', which this server does not hold.",
                        library)));
            }
        }
    }

    /// <summary>
    /// The range is the fields the register declares as moving. Narrowing what
    /// moves is an operator's to make; the set they narrow is not.
    /// </summary>
    private static void ExcludedFields(PluginConfiguration configuration, List<ConfigurationProblem> problems)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in configuration.ExcludedFields)
        {
            if (!seen.Add(field))
            {
                problems.Add(new ConfigurationProblem(
                    nameof(PluginConfiguration.ExcludedFields),
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ExcludedFields names the field '{0}' more than once.",
                        field)));
                continue;
            }

            var row = FieldRegister.Find(field);

            if (row is null)
            {
                problems.Add(new ConfigurationProblem(
                    nameof(PluginConfiguration.ExcludedFields),
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ExcludedFields names the field '{0}', which the field register does not declare.",
                        field)));
                continue;
            }

            if (!row.Moves)
            {
                problems.Add(new ConfigurationProblem(
                    nameof(PluginConfiguration.ExcludedFields),
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ExcludedFields names the field '{0}', which the field register already refuses to move: {1}",
                        field,
                        row.Reason)));
            }
        }
    }

    /// <summary>
    /// The range is any value, including the empty one. What is refused is not
    /// a value but a combination: no pairing chosen, beside a configuration
    /// that names libraries or fields to act on.
    /// </summary>
    /// <remarks>
    /// This is checked last so the sentence about a missing pairing arrives
    /// under the settings that made it a problem rather than above them.
    /// </remarks>
    private static void PairingId(PluginConfiguration configuration, List<ConfigurationProblem> problems)
    {
        if (configuration.PairingId != Guid.Empty)
        {
            return;
        }

        if (configuration.ParticipatingLibraries.Count == 0 && configuration.ExcludedFields.Count == 0)
        {
            return;
        }

        problems.Add(new ConfigurationProblem(
            nameof(PluginConfiguration.PairingId),
            "PairingId names no pairing, and the rest of this configuration names libraries or fields to act on."));
    }
}
