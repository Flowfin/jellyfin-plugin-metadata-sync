using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// The comparison rules for provider identifiers, and the comparison itself.
/// </summary>
/// <remarks>
/// Neither the keys nor the values are normalised for us. They are written by
/// whichever providers an operator enabled, at different times, by different
/// versions, on two servers that were never coordinated. So the rules are per
/// provider, they are written down, and they are a table rather than a chain of
/// conditionals: a chain is where a matcher quietly becomes wrong, because the
/// case that was handled and the case that fell through look the same.
/// <para>
/// This is pure. It reads the two dictionaries it is handed and nothing else -
/// no clock, no file, no transport - so every rule is testable from a table with
/// nothing running.
/// </para>
/// <para>
/// #27 assumes the pairing plugin owns resolution. This table is what this
/// plugin contributes to that conversation and what its own tests assert
/// against, so a change on either side is visible rather than absorbed.
/// </para>
/// </remarks>
public static class ProviderIdentifiers
{
    /// <summary>
    /// The table that ships inside this assembly.
    /// </summary>
    internal const string EmbeddedResourceName = "Jellyfin.Plugin.MetadataSync.Matching.provider-identifiers.json";

    private static readonly Lazy<TableContents> _table = new(() => Load(EmbeddedResourceName));

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>
    /// Gets one rule per provider the table names, in the order it writes them.
    /// </summary>
    public static IReadOnlyList<ProviderIdentifierRule> Rules => _table.Value.Rules;

    /// <summary>
    /// Gets the rule for a provider the table does not name.
    /// </summary>
    public static ProviderIdentifierRule DefaultRule => _table.Value.DefaultRule;

    /// <summary>
    /// Gets how a provider name is compared, which is one rule for every row.
    /// </summary>
    public static StringComparison KeyComparison => ComparisonFrom(_table.Value.KeyComparison);

    /// <summary>
    /// Returns the rule that decides a provider, which is the table's row for it
    /// or the default where it has none.
    /// </summary>
    /// <param name="provider">The provider, as the dictionary key spells it.</param>
    /// <returns>The rule.</returns>
    public static ProviderIdentifierRule RuleFor(string provider) =>
        Rules.FirstOrDefault(rule => string.Equals(rule.Provider, provider, KeyComparison)) ?? DefaultRule;

    /// <summary>
    /// Compares two items' provider identifiers.
    /// </summary>
    /// <param name="local">The identifiers on this server's item.</param>
    /// <param name="peer">The identifiers the peer sent.</param>
    /// <returns>What the comparison established.</returns>
    /// <remarks>
    /// Disagreement wins over agreement and there is no first-match-wins
    /// fallthrough. Every provider present on both sides is compared, and one
    /// that disagrees decides the whole comparison however many others agree:
    /// two identifiers naming different works is evidence that the items are
    /// different, and two naming the same work is not evidence against it.
    /// </remarks>
    public static ProviderIdentifierVerdict Compare(
        IReadOnlyDictionary<string, string> local,
        IReadOnlyDictionary<string, string> peer)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(peer);

        var agreed = false;

        foreach (var pair in local)
        {
            if (!TryFind(peer, pair.Key, out var theirs))
            {
                continue;
            }

            var rule = RuleFor(pair.Key);
            if (string.Equals(Normalise(pair.Value, rule), Normalise(theirs, rule), ComparisonFrom(rule.ValueComparison)))
            {
                agreed = true;
            }
            else
            {
                return ProviderIdentifierVerdict.Disagreement;
            }
        }

        return agreed ? ProviderIdentifierVerdict.Match : ProviderIdentifierVerdict.NoBasis;
    }

    /// <summary>
    /// Applies one rule's whitespace and normalisation handling to a value.
    /// </summary>
    /// <param name="value">The identifier as it was stored.</param>
    /// <param name="rule">The rule that decides this provider.</param>
    /// <returns>The value the comparison actually sees.</returns>
    internal static string Normalise(string? value, ProviderIdentifierRule rule)
    {
        var text = value ?? string.Empty;

        if (rule.Trimmed)
        {
            text = text.Trim();
        }

        if (string.Equals(rule.Normalisation, "LeadingZeros", StringComparison.Ordinal) && IsAllDigits(text))
        {
            text = text.TrimStart('0');
            if (text.Length == 0)
            {
                text = "0";
            }
        }

        return text;
    }

    internal static TableContents Load(string resourceName)
    {
        var assembly = typeof(ProviderIdentifiers).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException(NoTableEmbedded(resourceName));

        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return Parse(Encoding.UTF8.GetString(bytes));
    }

    internal static TableContents Parse(string text)
    {
        var read = JsonSerializer.Deserialize<TableFile>(text, _json) ?? throw new InvalidOperationException(NothingToRead());

        var rules = new List<ProviderIdentifierRule>(read.Rules.Count);
        foreach (var row in read.Rules)
        {
            rules.Add(Row(row));
        }

        return new TableContents(
            read.KeyComparison,
            read.KeyComparisonReason,
            new ReadOnlyCollection<ProviderIdentifierRule>(rules),
            Row(read.DefaultRule));
    }

    private static ProviderIdentifierRule Row(TableRow row) => new(
        row.Provider,
        row.ValueComparison,
        row.Trimmed,
        row.Normalisation,
        row.Reason);

    private static bool TryFind(IReadOnlyDictionary<string, string> identifiers, string provider, out string value)
    {
        foreach (var pair in identifiers)
        {
            if (string.Equals(pair.Key, provider, KeyComparison))
            {
                value = pair.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool IsAllDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var character in text)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static StringComparison ComparisonFrom(string comparison) => string.Equals(comparison, "OrdinalIgnoreCase", StringComparison.Ordinal)
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static string NoTableEmbedded(string resourceName) =>
        "No provider identifier table is embedded under '" + resourceName + "', so no comparison rule is declared.";

    private static string NothingToRead() =>
        "The provider identifier table text describes no table, so no comparison rule is declared.";

    /// <summary>
    /// One reading of the table.
    /// </summary>
    internal sealed class TableContents
    {
        internal TableContents(string keyComparison, string keyComparisonReason, IReadOnlyList<ProviderIdentifierRule> rules, ProviderIdentifierRule defaultRule)
        {
            KeyComparison = keyComparison;
            KeyComparisonReason = keyComparisonReason;
            Rules = rules;
            DefaultRule = defaultRule;
        }

        /// <summary>Gets how a provider name is compared.</summary>
        internal string KeyComparison { get; }

        /// <summary>Gets why provider names are compared that way.</summary>
        internal string KeyComparisonReason { get; }

        /// <summary>Gets the rows.</summary>
        internal IReadOnlyList<ProviderIdentifierRule> Rules { get; }

        /// <summary>Gets the rule for a provider with no row.</summary>
        internal ProviderIdentifierRule DefaultRule { get; }
    }

    internal sealed class TableRow
    {
        [JsonPropertyName("provider")]
        public string Provider { get; init; } = string.Empty;

        [JsonPropertyName("valueComparison")]
        public string ValueComparison { get; init; } = string.Empty;

        [JsonPropertyName("trimmed")]
        public bool Trimmed { get; init; }

        [JsonPropertyName("normalisation")]
        public string Normalisation { get; init; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = string.Empty;
    }

    private sealed class TableFile
    {
        [JsonPropertyName("keyComparison")]
        public string KeyComparison { get; init; } = string.Empty;

        [JsonPropertyName("keyComparisonReason")]
        public string KeyComparisonReason { get; init; } = string.Empty;

        [JsonPropertyName("defaultRule")]
        public TableRow DefaultRule { get; init; } = new();

        [JsonPropertyName("rules")]
        public List<TableRow> Rules { get; init; } = new();
    }
}
