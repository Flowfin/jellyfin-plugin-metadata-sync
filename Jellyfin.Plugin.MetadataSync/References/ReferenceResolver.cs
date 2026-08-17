using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetadataSync.References;

/// <summary>
/// Decides whether a reference arriving from the peer is already here, and says
/// so in one of a closed set of outcomes. One function per kind of reference,
/// each reading that kind's rows out of the table that ships in the assembly.
/// </summary>
/// <remarks>
/// This is pure. It reads the incoming value and the entries this server
/// already holds, and nothing else: no library, no clock, no file, no
/// transport. Every rule below is therefore decidable from a table with nothing
/// running, which is what lets the comparison be argued before anything can
/// create an entry from it.
/// <para>
/// Nothing here creates anything. <see cref="ReferenceOutcome.Create"/> says
/// what would be created and no more, because an entry created by a sync has to
/// carry a mark saying so and there is nowhere yet to keep one. That store is
/// #47, and until it exists an outcome is something a plan shows rather than
/// something a pass does.
/// </para>
/// <para>
/// The table is an embedded resource for the same reason the field register and
/// the conflict rules are. A comparison rule an operator can edit per
/// installation is not a rule, and this one decides whether two spellings of
/// one person are one person.
/// </para>
/// </remarks>
public static class ReferenceResolver
{
    /// <summary>
    /// The comparison table that ships inside this assembly.
    /// </summary>
    internal const string EmbeddedResourceName = "Jellyfin.Plugin.MetadataSync.References.reference-comparison.json";

    private static readonly Lazy<IReadOnlyList<ComparisonRule>> _rules = new(() => Load(EmbeddedResourceName));

    private static readonly Lazy<IReadOnlyDictionary<string, ComparisonRule>> _byPair = new(() => Indexed(_rules.Value));

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>
    /// Gets every declared row, in the order the table writes them.
    /// </summary>
    public static IReadOnlyList<ComparisonRule> Rules => _rules.Value;

    /// <summary>
    /// Returns the row that answers one property for one kind.
    /// </summary>
    /// <param name="kind">The kind of reference.</param>
    /// <param name="property">The way two spellings differ.</param>
    /// <returns>The row.</returns>
    /// <remarks>
    /// There is no refusal here and no default. Every pair has a row or the
    /// table did not load at all, which is <see cref="Parse"/>'s last check, so
    /// a lookup that could miss would be a second answer to a question already
    /// answered where it can be proved.
    /// </remarks>
    public static ComparisonRule RuleFor(ReferenceKind kind, ReferenceProperty property) =>
        _byPair.Value[Pair(kind, property)];

    /// <summary>
    /// Resolves one incoming genre against the genres this server holds.
    /// </summary>
    /// <param name="incoming">The genre the peer sent.</param>
    /// <param name="here">The genres this server already holds, as it spells them.</param>
    /// <returns>What was established.</returns>
    public static ReferenceResolution ResolveGenre(string incoming, IReadOnlyCollection<string> here) =>
        Decide(ReferenceKind.Genre, incoming, here);

    /// <summary>
    /// Resolves one incoming studio against the studios this server holds.
    /// </summary>
    /// <param name="incoming">The studio the peer sent.</param>
    /// <param name="here">The studios this server already holds, as it spells them.</param>
    /// <returns>What was established.</returns>
    public static ReferenceResolution ResolveStudio(string incoming, IReadOnlyCollection<string> here) =>
        Decide(ReferenceKind.Studio, incoming, here);

    /// <summary>
    /// Resolves one incoming person against the people this server holds.
    /// </summary>
    /// <param name="incoming">The person's name, as the peer spells it.</param>
    /// <param name="here">The names this server already holds.</param>
    /// <returns>What was established.</returns>
    /// <remarks>
    /// A name and nothing else. The role somebody plays belongs to one item
    /// rather than to the person, so it decides nothing about whether this
    /// person is already here, and passing it in would invite a rule that read
    /// it.
    /// </remarks>
    public static ReferenceResolution ResolvePerson(string incoming, IReadOnlyCollection<string> here) =>
        Decide(ReferenceKind.Person, incoming, here);

    /// <summary>
    /// Reads a comparison table out of this assembly by resource name.
    /// </summary>
    /// <param name="resourceName">The embedded resource to read.</param>
    /// <returns>The rows, in the order the table writes them.</returns>
    internal static IReadOnlyList<ComparisonRule> Load(string resourceName)
    {
        var assembly = typeof(ReferenceResolver).Assembly;
        // The refusal sits on the same line as the read for the reason the
        // conflict rules give: a refusal is named by the line of code that
        // refuses, and a throw continued onto its own line reports the line
        // above it.
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException(NoTableEmbedded(resourceName));

        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return Parse(Encoding.UTF8.GetString(bytes));
    }

    /// <summary>
    /// Turns table text into rows, refusing a table that leaves any pair of
    /// kind and property unanswered.
    /// </summary>
    /// <param name="text">The comparison table, as JSON.</param>
    /// <returns>The rows, in the order the table writes them.</returns>
    /// <remarks>
    /// Completeness is checked against the two enumerations rather than against
    /// a count written in the file, so adding a kind or a property makes every
    /// route red until the table answers for it. A default for an unanswered
    /// pair is what this refuses: whichever default was chosen would decide
    /// whether two spellings of a person are one person, without a row anybody
    /// could disagree with.
    /// </remarks>
    internal static IReadOnlyList<ComparisonRule> Parse(string text)
    {
        var read = JsonSerializer.Deserialize<TableFile>(text, _json) ?? throw new InvalidOperationException(NothingToRead());

        var rules = new List<ComparisonRule>(read.Rules.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in read.Rules)
        {
            // Parsed by name and never by number, for the reason the conflict
            // rules give: a table saying 2 declares nothing a reader can check.
            if (Array.IndexOf(Enum.GetNames<ReferenceKind>(), row.Kind) < 0)
            {
                throw new InvalidOperationException(NoSuchKind(row));
            }

            if (Array.IndexOf(Enum.GetNames<ReferenceProperty>(), row.Property) < 0)
            {
                throw new InvalidOperationException(NoSuchProperty(row));
            }

            if (Array.IndexOf(Enum.GetNames<ReferenceAnswer>(), row.Answer) < 0)
            {
                throw new InvalidOperationException(NoSuchAnswer(row));
            }

            if (string.IsNullOrWhiteSpace(row.Reason))
            {
                throw new InvalidOperationException(NotArgued(row));
            }

            if (!seen.Add(row.Kind + "/" + row.Property))
            {
                throw new InvalidOperationException(TwoRowsForOnePair(row));
            }

            rules.Add(new ComparisonRule(
                Enum.Parse<ReferenceKind>(row.Kind, ignoreCase: false),
                Enum.Parse<ReferenceProperty>(row.Property, ignoreCase: false),
                Enum.Parse<ReferenceAnswer>(row.Answer, ignoreCase: false),
                row.Reason));
        }

        foreach (var kind in Enum.GetValues<ReferenceKind>())
        {
            foreach (var property in Enum.GetValues<ReferenceProperty>())
            {
                if (!seen.Contains(Pair(kind, property)))
                {
                    throw new InvalidOperationException(NoRowForPair(kind, property));
                }
            }
        }

        return new ReadOnlyCollection<ComparisonRule>(rules);
    }

    /// <summary>
    /// Applies one kind's rules to a value and returns what the comparison
    /// actually sees.
    /// </summary>
    /// <param name="value">The reference as it was written.</param>
    /// <param name="kind">The kind whose rows decide the folding.</param>
    /// <param name="includeUndecided">
    /// Whether to fold the properties the table refuses to decide as well. False
    /// asks whether two values are the same reference; true asks whether they
    /// are close enough that nobody here may say.
    /// </param>
    /// <returns>The folded value.</returns>
    private static string Fold(string value, ReferenceKind kind, bool includeUndecided)
    {
        // Composed first, always, and this is not one of the four rules. Two
        // encodings of one character are one character, so folding them is not
        // a decision about spelling at all.
        var text = value.Normalize(NormalizationForm.FormC);

        if (Folds(kind, ReferenceProperty.Whitespace, includeUndecided))
        {
            text = CollapsedSpace(text);
        }

        if (Folds(kind, ReferenceProperty.Punctuation, includeUndecided))
        {
            text = CollapsedSpace(WithoutPunctuation(text));
        }

        if (Folds(kind, ReferenceProperty.Accents, includeUndecided))
        {
            text = WithoutAccents(text);
        }

        if (Folds(kind, ReferenceProperty.Case, includeUndecided))
        {
            text = text.ToUpperInvariant();
        }

        return text;
    }

    private static ReferenceResolution Decide(ReferenceKind kind, string incoming, IReadOnlyCollection<string> here)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(here);

        if (string.IsNullOrWhiteSpace(incoming))
        {
            return new ReferenceResolution(ReferenceOutcome.Refused, null, Array.Empty<string>(), NotAReference(kind));
        }

        var same = new List<string>();
        var near = new List<string>();

        var strict = Fold(incoming, kind, includeUndecided: false);
        var loose = Fold(incoming, kind, includeUndecided: true);

        foreach (var entry in here)
        {
            if (string.Equals(Fold(entry, kind, includeUndecided: false), strict, StringComparison.Ordinal))
            {
                same.Add(entry);
            }
            else if (string.Equals(Fold(entry, kind, includeUndecided: true), loose, StringComparison.Ordinal))
            {
                near.Add(entry);
            }
        }

        // An exact match settles it even where something else here is close.
        // The near miss is somebody else's duplicate and this reference is not
        // the place it gets discovered.
        if (same.Count == 1)
        {
            return new ReferenceResolution(ReferenceOutcome.Resolved, same[0], Array.Empty<string>(), Resolved(kind, incoming, same[0]));
        }

        if (same.Count > 1)
        {
            return new ReferenceResolution(ReferenceOutcome.Undecided, null, new ReadOnlyCollection<string>(same), AlreadyDoubled(kind, incoming));
        }

        if (near.Count > 0)
        {
            return new ReferenceResolution(ReferenceOutcome.Undecided, null, new ReadOnlyCollection<string>(near), TooCloseToDecide(kind, incoming));
        }

        return new ReferenceResolution(ReferenceOutcome.Create, null, Array.Empty<string>(), NothingLikeIt(kind, incoming));
    }

    private static IReadOnlyDictionary<string, ComparisonRule> Indexed(IReadOnlyList<ComparisonRule> rules)
    {
        var byPair = new Dictionary<string, ComparisonRule>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            byPair[Pair(rule.Kind, rule.Property)] = rule;
        }

        return byPair;
    }

    private static bool Folds(ReferenceKind kind, ReferenceProperty property, bool includeUndecided)
    {
        var answer = RuleFor(kind, property).Answer;
        return answer == ReferenceAnswer.Same || (includeUndecided && answer == ReferenceAnswer.Undecided);
    }

    private static string CollapsedSpace(string text)
    {
        var built = new StringBuilder(text.Length);
        var pending = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pending = built.Length > 0;
                continue;
            }

            if (pending)
            {
                built.Append(' ');
                pending = false;
            }

            built.Append(character);
        }

        return built.ToString();
    }

    // Replaced by a space rather than removed, because a hyphen between two
    // words stands in for one. Removing it would make "Sci-Fi" and "Sci Fi"
    // differ by a space that the punctuation rule is the reason for.
    private static string WithoutPunctuation(string text)
    {
        var built = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            built.Append(char.IsPunctuation(character) || char.IsSymbol(character) ? ' ' : character);
        }

        return built.ToString();
    }

    private static string WithoutAccents(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var built = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                built.Append(character);
            }
        }

        return built.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string Pair(ReferenceKind kind, ReferenceProperty property) =>
        kind + "/" + property;

    private static string NotAReference(ReferenceKind kind) => string.Format(
        CultureInfo.InvariantCulture,
        "The incoming {0} is empty or is nothing but space, so there is no reference to resolve. It is refused with this reason rather than passed over.",
        kind);

    private static string Resolved(ReferenceKind kind, string incoming, string match) => string.Format(
        CultureInfo.InvariantCulture,
        "The incoming {0} '{1}' is the one this server already holds as '{2}'.",
        kind,
        incoming,
        match);

    private static string AlreadyDoubled(ReferenceKind kind, string incoming) => string.Format(
        CultureInfo.InvariantCulture,
        "This server holds more than one {0} that the incoming '{1}' is the same as. Which of them a value is written against is not decidable from the names, so nothing is written.",
        kind,
        incoming);

    private static string TooCloseToDecide(ReferenceKind kind, string incoming) => string.Format(
        CultureInfo.InvariantCulture,
        "The incoming {0} '{1}' differs from something this server already holds only in a way this plugin does not decide. Joining them would drop one spelling and creating it would hold one thing twice.",
        kind,
        incoming);

    private static string NothingLikeIt(ReferenceKind kind, string incoming) => string.Format(
        CultureInfo.InvariantCulture,
        "This server holds no {0} resembling the incoming '{1}', so the reference would be created here and marked as having arrived from a sync.",
        kind,
        incoming);

    private static string NoTableEmbedded(string resourceName) => string.Format(
        CultureInfo.InvariantCulture,
        "No reference comparison table is embedded under '{0}', so nothing declares when two spellings are one reference.",
        resourceName);

    private static string NothingToRead() =>
        "The reference comparison text describes no table, so nothing declares when two spellings are one reference.";

    private static string NoSuchKind(TableRow row) => string.Format(
        CultureInfo.InvariantCulture,
        "A row answers for the reference kind '{0}', which is not a kind this plugin resolves.",
        row.Kind);

    private static string NoSuchProperty(TableRow row) => string.Format(
        CultureInfo.InvariantCulture,
        "The row for '{0}' answers for the property '{1}', which is not one of the ways two spellings may differ.",
        row.Kind,
        row.Property);

    private static string NoSuchAnswer(TableRow row) => string.Format(
        CultureInfo.InvariantCulture,
        "The row for '{0}' and '{1}' answers '{2}', which is not an answer the table may give.",
        row.Kind,
        row.Property,
        row.Answer);

    private static string NotArgued(TableRow row) => string.Format(
        CultureInfo.InvariantCulture,
        "The row for '{0}' and '{1}' carries no reason, so nothing says why that answer is right.",
        row.Kind,
        row.Property);

    private static string TwoRowsForOnePair(TableRow row) => string.Format(
        CultureInfo.InvariantCulture,
        "Two rows answer for '{0}' and '{1}'. Whichever is read second would decide silently.",
        row.Kind,
        row.Property);

    private static string NoRowForPair(ReferenceKind kind, ReferenceProperty property) => string.Format(
        CultureInfo.InvariantCulture,
        "No row answers what a difference in {1} means for a {0}, and there is no default: the answer would decide whether two spellings are one reference with nothing written down to disagree with.",
        kind,
        property);

    private sealed class TableFile
    {
        [JsonPropertyName("rules")]
        public List<TableRow> Rules { get; init; } = new();
    }

    private sealed class TableRow
    {
        [JsonPropertyName("kind")]
        public string Kind { get; init; } = string.Empty;

        [JsonPropertyName("property")]
        public string Property { get; init; } = string.Empty;

        [JsonPropertyName("answer")]
        public string Answer { get; init; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = string.Empty;
    }
}
