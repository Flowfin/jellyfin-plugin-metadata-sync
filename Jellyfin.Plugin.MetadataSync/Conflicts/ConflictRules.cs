using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetadataSync.Conflicts;

/// <summary>
/// The declared conflict rules, in evaluation order, read from the table that
/// ships inside the assembly. This is the only place a rule is declared: the
/// document under docs/ is a rendering of these rows and the suite holds the
/// two against each other.
/// </summary>
/// <remarks>
/// The rules are data and they were declared before the resolver was written,
/// which is the order this was planned in rather than an accident. A rule set
/// derived from an implementation is a description of what somebody wrote; a
/// rule set the implementation is derived from is something a reviewer can
/// disagree with before any of it is built.
/// <para>
/// The order these rows are in is part of the declaration and not a detail of
/// whoever evaluates them. <see cref="ConflictResolver"/> reads the sequence
/// out of this list on every call, so moving a row here changes what the plugin
/// does.
/// </para>
/// <para>
/// It is an embedded resource for the same reason the field register is. A
/// plugin directory is a place an operator can edit, and a conflict rule an
/// operator can edit is not a rule.
/// </para>
/// <para>
/// This type reads a stream out of its own assembly and touches no file, which
/// is also what keeps the invariant lint quiet. The lint matches a spelling and
/// the rule it carries is about deciding item identity, so reading an embedded
/// resource is outside that rule rather than hidden from it.
/// </para>
/// </remarks>
public static class ConflictRules
{
    /// <summary>
    /// The rule table that ships inside this assembly.
    /// </summary>
    internal const string EmbeddedResourceName = "Jellyfin.Plugin.MetadataSync.Conflicts.conflict-rules.json";

    private static readonly Lazy<IReadOnlyList<ConflictRule>> _rules = new(() => Load(EmbeddedResourceName));

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>
    /// Gets every declared rule, in the order they are evaluated in. The order
    /// is the order of the table and never a sort applied afterwards, because a
    /// rule set whose order is derived is a rule set whose order can change
    /// without anybody editing it.
    /// </summary>
    public static IReadOnlyList<ConflictRule> Rules => _rules.Value;

    /// <summary>
    /// Finds a rule by the name it is referred to by.
    /// </summary>
    /// <param name="id">The rule name.</param>
    /// <returns>The rule, or null where no rule carries that name.</returns>
    public static ConflictRule? Find(string id) =>
        Rules.FirstOrDefault(rule => string.Equals(rule.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Reads a rule table out of this assembly by resource name.
    /// </summary>
    /// <param name="resourceName">The embedded resource to read.</param>
    /// <returns>The rules, in the order the table writes them.</returns>
    internal static IReadOnlyList<ConflictRule> Load(string resourceName)
    {
        var assembly = typeof(ConflictRules).Assembly;
        // The refusal sits on the same line as the read on purpose. The suite
        // names a refusal site by the line of code that refuses, and a throw
        // continued onto its own line is reported at the line above it.
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException(NoRulesEmbedded(resourceName));

        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return Parse(Encoding.UTF8.GetString(bytes));
    }

    /// <summary>
    /// Turns rule text into rules, refusing text that does not describe a rule
    /// set anybody could evaluate.
    /// </summary>
    /// <param name="text">The rule table, as JSON.</param>
    /// <returns>The rules, in the order the table writes them.</returns>
    internal static IReadOnlyList<ConflictRule> Parse(string text)
    {
        var read = JsonSerializer.Deserialize<RuleFile>(text, _json) ?? throw new InvalidOperationException(NothingToRead());

        var rules = new List<ConflictRule>(read.Rules.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in read.Rules)
        {
            // Parsed by name and never by number, because Enum.TryParse reads
            // "2" as a member and a table saying 2 declares nothing a reader
            // can check.
            if (Array.IndexOf(Enum.GetNames<ConflictOutcome>(), row.Outcome) < 0)
            {
                throw new InvalidOperationException(NoSuchOutcome(row));
            }

            if (string.IsNullOrWhiteSpace(row.Condition) || string.IsNullOrWhiteSpace(row.Reason))
            {
                throw new InvalidOperationException(NotArgued(row));
            }

            if (!seen.Add(row.Id))
            {
                throw new InvalidOperationException(TwoRulesUnderOneName(row));
            }

            rules.Add(new ConflictRule(
                row.Id,
                row.Condition,
                Enum.Parse<ConflictOutcome>(row.Outcome, ignoreCase: false),
                row.Reason));
        }

        return new ReadOnlyCollection<ConflictRule>(rules);
    }

    private static string NoRulesEmbedded(string resourceName) => string.Format(
        CultureInfo.InvariantCulture,
        "No conflict rule table is embedded under '{0}', so no rule is declared and every difference is refused.",
        resourceName);

    private static string NothingToRead() =>
        "The conflict rule text describes no rule set, so no rule is declared and every difference is refused.";

    private static string NoSuchOutcome(RuleRow row) => string.Format(
        CultureInfo.InvariantCulture,
        "The rule '{0}' produces '{1}', which is not one of the outcomes a rule may produce.",
        row.Id,
        row.Outcome);

    private static string NotArgued(RuleRow row) => string.Format(
        CultureInfo.InvariantCulture,
        "The rule '{0}' declares no condition or no reason, so nothing says when it fires or why it is right.",
        row.Id);

    private static string TwoRulesUnderOneName(RuleRow row) => string.Format(
        CultureInfo.InvariantCulture,
        "Two rules are named '{0}'. A rule is reported by its name, so a name held by two rules names neither.",
        row.Id);

    private sealed class RuleFile
    {
        [JsonPropertyName("rules")]
        public List<RuleRow> Rules { get; init; } = new();
    }

    private sealed class RuleRow
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("condition")]
        public string Condition { get; init; } = string.Empty;

        [JsonPropertyName("outcome")]
        public string Outcome { get; init; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = string.Empty;
    }
}
