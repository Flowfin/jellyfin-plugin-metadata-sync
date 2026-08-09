using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.MetadataSync.References;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Reference resolution, held up against the document that declares it.
/// </summary>
/// <remarks>
/// The fixture rows are read out of `docs/references.md` rather than restated
/// here, for the reason the provider identifier suite gives: a table of
/// expectations written twice is a table that drifts, and the copy a reader
/// trusts is the document.
/// <para>
/// Every case runs against the resolution with no substitutes of any kind. The
/// arguments are a string and a list of strings, so there is nothing here to
/// stand in for a library, and that is the property the first Done-when
/// condition of #15 asks for rather than a convenience of the test.
/// </para>
/// </remarks>
public class ReferenceResolutionTests
{
    /// <summary>
    /// A table declaring a kind this plugin does not resolve.
    /// </summary>
    public const string UnknownKindTable = """
        { "rules": [ { "kind": "Collection", "property": "Case", "answer": "Same", "reason": "The row is well formed apart from the kind." } ] }
        """;

    /// <summary>
    /// A table declaring a way of differing that nothing names.
    /// </summary>
    public const string UnknownPropertyTable = """
        { "rules": [ { "kind": "Genre", "property": "Spelling", "answer": "Same", "reason": "The row is well formed apart from the property." } ] }
        """;

    /// <summary>
    /// A table giving an answer outside the closed set.
    /// </summary>
    public const string UnknownAnswerTable = """
        { "rules": [ { "kind": "Genre", "property": "Case", "answer": "Different", "reason": "The row is well formed apart from the answer." } ] }
        """;

    /// <summary>
    /// A table whose row says what it decides and never why.
    /// </summary>
    public const string UnarguedTable = """
        { "rules": [ { "kind": "Genre", "property": "Case", "answer": "Same", "reason": "" } ] }
        """;

    /// <summary>
    /// A table answering one pair twice.
    /// </summary>
    public const string DoubleAnsweredTable = """
        {
          "rules": [
            { "kind": "Genre", "property": "Case", "answer": "Same", "reason": "The first answer, which a reader would find." },
            { "kind": "Genre", "property": "Case", "answer": "Undecided", "reason": "The second answer, which the comparison would use." }
          ]
        }
        """;

    /// <summary>
    /// A table that is valid as far as it goes and leaves eleven pairs
    /// unanswered.
    /// </summary>
    public const string IncompleteTable = """
        { "rules": [ { "kind": "Genre", "property": "Case", "answer": "Same", "reason": "One correct row, and nothing for any other pair." } ] }
        """;

    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "references.md");

    private static readonly string[] _oneGenre = new[] { "Comedy" };
    private static readonly string[] _oneStudio = new[] { "A24" };
    private static readonly string[] _oneNameWithItsAccents = new[] { "Zoë Saldaña" };
    private static readonly string[] _bothSpellingsOfOneName = new[] { "Zoë Saldaña", "Zoe Saldana" };
    private static readonly string[] _oneGenreHeldTwice = new[] { "sci-fi", "SCI-FI" };
    private static readonly string[] _theCandidateFirst = new[] { "Sci-Fi", "Drama" };
    private static readonly string[] _theCandidateLast = new[] { "Drama", "Sci-Fi" };

    private const string FixtureHeader =
        "| Case | Reference | Incoming | Already here | Outcome | The difference it is about |";

    /// <summary>
    /// Every row of the fixture table in the document, as the document writes
    /// it. A row added there is a test here without anybody remembering to add
    /// one, which is the direction that fails closed.
    /// </summary>
    /// <returns>The rows.</returns>
    public static TheoryData<string, string, string, string, string, string> FixtureRows()
    {
        var data = new TheoryData<string, string, string, string, string, string>();

        foreach (var row in TableRows(FixtureHeader))
        {
            data.Add(row[0], row[1], row[2], row[3], row[4], row[5]);
        }

        return data;
    }

    /// <summary>
    /// One test per row of the fixture table, with its expected outcome quoted
    /// from the document a reader is expected to trust.
    /// </summary>
    /// <param name="description">What the row is a case of.</param>
    /// <param name="kind">The kind of reference.</param>
    /// <param name="incoming">What the peer sent.</param>
    /// <param name="here">What this server already holds.</param>
    /// <param name="expected">The outcome the document states.</param>
    /// <param name="difference">The difference the case is about, or none.</param>
    [Theory]
    [MemberData(nameof(FixtureRows))]
    public void TheResolutionAnswersWhatTheDocumentSaysItAnswers(
        string description,
        string kind,
        string incoming,
        string here,
        string expected,
        string difference)
    {
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.False(string.IsNullOrWhiteSpace(difference));

        var resolution = Resolve(kind, incoming, Entries(here));

        Assert.Equal(Enum.Parse<ReferenceOutcome>(Unquote(expected), ignoreCase: false), resolution.Outcome);
    }

    /// <summary>
    /// A case about one of the four differences expects what the table answers
    /// for that difference. This is what makes the fixtures derived from the
    /// rules rather than agreeing with them by hand: flipping a row's answer
    /// reds the case as well as the run.
    /// </summary>
    /// <param name="description">What the row is a case of.</param>
    /// <param name="kind">The kind of reference.</param>
    /// <param name="incoming">What the peer sent.</param>
    /// <param name="here">What this server already holds.</param>
    /// <param name="expected">The outcome the document states.</param>
    /// <param name="difference">The difference the case is about, or none.</param>
    [Theory]
    [MemberData(nameof(FixtureRows))]
    public void ACaseAboutADifferenceExpectsWhatTheTableAnswersForIt(
        string description,
        string kind,
        string incoming,
        string here,
        string expected,
        string difference)
    {
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.False(string.IsNullOrWhiteSpace(incoming));
        Assert.NotNull(here);

        if (string.Equals(Unquote(difference), "-", StringComparison.Ordinal))
        {
            return;
        }

        var rule = ReferenceResolver.RuleFor(
            Enum.Parse<ReferenceKind>(Unquote(kind), ignoreCase: false),
            Enum.Parse<ReferenceProperty>(Unquote(difference), ignoreCase: false));

        var owed = rule.Answer == ReferenceAnswer.Same
            ? ReferenceOutcome.Resolved
            : ReferenceOutcome.Undecided;

        Assert.Equal(owed, Enum.Parse<ReferenceOutcome>(Unquote(expected), ignoreCase: false));
    }

    /// <summary>
    /// Every declared row has a case behind it. A table that grew a row without
    /// a fixture would otherwise pass every fixture it kept.
    /// </summary>
    [Fact]
    public void EveryDeclaredRowHasACaseAboutIt()
    {
        var covered = TableRows(FixtureHeader)
            .Where(r => !string.Equals(Unquote(r[5]), "-", StringComparison.Ordinal))
            .Select(r => Unquote(r[1]) + "/" + Unquote(r[5]))
            .ToHashSet(StringComparer.Ordinal);

        var uncovered = ReferenceResolver.Rules
            .Select(rule => rule.Kind + "/" + rule.Property)
            .Where(pair => !covered.Contains(pair))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(uncovered);
    }

    /// <summary>
    /// The four outcomes each have a case as well, because a fixture table
    /// covering every rule can still never reach three quarters of the answers.
    /// </summary>
    [Fact]
    public void EveryOutcomeHasACaseThatProducesIt()
    {
        var produced = TableRows(FixtureHeader)
            .Select(r => Resolve(r[1], r[2], Entries(r[3])).Outcome)
            .ToHashSet();

        var unreached = Enum.GetValues<ReferenceOutcome>()
            .Where(outcome => !produced.Contains(outcome))
            .ToList();

        Assert.Empty(unreached);
    }

    /// <summary>
    /// The committed document is exactly what the source produces. Both
    /// directions: a row added to the source with no rendering fails, and a
    /// cell edited in the document that the source does not produce fails too.
    /// </summary>
    [Fact]
    public void TheCommittedDocumentIsWhatTheSourceProduces()
    {
        var text = File.ReadAllText(_document).Replace("\r\n", "\n", StringComparison.Ordinal);

        foreach (var (name, produced) in Rendered())
        {
            Assert.Equal(produced, BlockIn(text, name));
        }
    }

    /// <summary>
    /// Every block the source produces is in the document, and no other block
    /// claims to be rendered from it.
    /// </summary>
    [Fact]
    public void EveryRenderedBlockIsPresentAndNoOtherIs()
    {
        var text = File.ReadAllText(_document).Replace("\r\n", "\n", StringComparison.Ordinal);

        var inDocument = text.Split('\n')
            .Where(l => l.StartsWith("<!-- rendered from reference-comparison.json: ", StringComparison.Ordinal))
            .Select(l => l["<!-- rendered from reference-comparison.json: ".Length..].Replace(" -->", string.Empty, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Rendered().Keys.Order(StringComparer.Ordinal).ToList(), inDocument);
    }

    /// <summary>
    /// Every row is argued. A row with no reason is a rule nobody can disagree
    /// with later, which is how a comparison acquires a folding nobody meant.
    /// </summary>
    [Fact]
    public void EveryRowCarriesAReason()
    {
        Assert.NotEmpty(ReferenceResolver.Rules);
        Assert.All(ReferenceResolver.Rules, r => Assert.True(r.Reason.Length > 40, r.Kind + "/" + r.Property));
    }

    /// <summary>
    /// The ordinary case, and the neighbour the two refusals below differ from
    /// by one thing.
    /// </summary>
    [Fact]
    public void AGenreThisServerAlreadyHoldsResolvesToIt()
    {
        var resolution = ReferenceResolver.ResolveGenre("Comedy", _oneGenre);

        Assert.Equal(ReferenceOutcome.Resolved, resolution.Outcome);
        Assert.Equal("Comedy", resolution.Match);
    }

    /// <summary>
    /// A resolution names the entry it resolved to as this server spells it,
    /// and not as the peer spelled it. Writing the peer's spelling back would
    /// rename the operator's entry through a field that only meant to point at
    /// it.
    /// </summary>
    [Fact]
    public void AResolutionNamesTheSpellingThisServerHolds()
    {
        var resolution = ReferenceResolver.ResolveStudio("a24", _oneStudio);

        Assert.Equal("A24", resolution.Match);
    }

    /// <summary>
    /// An exact match settles the outcome even where something else here is
    /// close. The near miss is this server's own duplicate, and one incoming
    /// reference is not where it gets discovered.
    /// </summary>
    [Fact]
    public void AnExactMatchWinsOverANearMissBesideIt()
    {
        var resolution = ReferenceResolver.ResolvePerson("Zoe Saldana", _bothSpellingsOfOneName);

        Assert.Equal(ReferenceOutcome.Resolved, resolution.Outcome);
        Assert.Equal("Zoe Saldana", resolution.Match);
    }

    /// <summary>
    /// Every outcome carries a sentence. An outcome without one is a row in a
    /// register an operator cannot act on.
    /// </summary>
    [Fact]
    public void EveryOutcomeCarriesASentence()
    {
        foreach (var row in TableRows(FixtureHeader))
        {
            var resolution = Resolve(row[1], row[2], Entries(row[3]));

            Assert.True(resolution.Reason.Length > 40, row[0]);
        }
    }

    /// <summary>
    /// An undecided outcome names what made it undecidable. A report saying
    /// only that something was close is a row an operator cannot act on.
    /// </summary>
    [Fact]
    public void AnUndecidedOutcomeNamesTheEntriesBehindIt()
    {
        var near = ReferenceResolver.ResolvePerson("Zoe Saldana", _oneNameWithItsAccents);
        var doubled = ReferenceResolver.ResolveGenre("Sci-Fi", _oneGenreHeldTwice);

        Assert.Equal(_oneNameWithItsAccents, near.Candidates);
        Assert.Equal(_oneGenreHeldTwice, doubled.Candidates);
    }

    /// <summary>
    /// A resolution reads the two arguments and nothing else, so the same
    /// arguments answer the same thing however they are ordered or repeated.
    /// </summary>
    [Fact]
    public void TheOutcomeDoesNotDependOnTheOrderTheEntriesArriveIn()
    {
        var forwards = ReferenceResolver.ResolveGenre("Sci Fi", _theCandidateFirst);
        var backwards = ReferenceResolver.ResolveGenre("Sci Fi", _theCandidateLast);

        Assert.Equal(forwards.Outcome, backwards.Outcome);
        Assert.Equal(forwards.Candidates, backwards.Candidates);
    }

    /// <summary>
    /// There is nothing to resolve. Reading a missing reference as an empty one
    /// would answer `Refused`, which is a real answer to a question that was
    /// never asked.
    /// </summary>
    [Fact]
    public void ResolvingAReferenceThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ReferenceResolver.ResolveGenre(null!, Array.Empty<string>()));
    }

    /// <summary>
    /// The same failure on the other side. An absent list of entries is not an
    /// empty library, and reading it as one would create every reference that
    /// arrived.
    /// </summary>
    [Fact]
    public void ResolvingAgainstEntriesThatAreNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ReferenceResolver.ResolveGenre("Comedy", null!));
    }

    /// <summary>
    /// The table that ships inside the assembly is the one the comparison runs
    /// on. This is the neighbour for the loader refusals below.
    /// </summary>
    [Fact]
    public void TheTableThatShipsInTheAssemblyLoads()
    {
        Assert.NotEmpty(ReferenceResolver.Load(ReferenceResolver.EmbeddedResourceName));
    }

    /// <summary>
    /// A build that did not embed the table declares nothing. Reading that as
    /// an empty table would leave every difference to a default nobody wrote.
    /// </summary>
    [Fact]
    public void ATableThatIsNotEmbeddedIsRefused()
    {
        Assert.Throws<InvalidOperationException>(
            () => ReferenceResolver.Load("Jellyfin.Plugin.MetadataSync.References.no-such-table.json"));
    }

    /// <summary>
    /// Table text that describes no table is refused rather than read as one
    /// with no rows.
    /// </summary>
    [Fact]
    public void TableTextThatDescribesNoTableIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => ReferenceResolver.Parse("null"));
    }

    /// <summary>
    /// A row answering for a kind this plugin does not resolve is refused. A
    /// table that quietly ignored it would answer for three kinds while
    /// declaring four.
    /// </summary>
    [Fact]
    public void ARowNamingAKindThisPluginDoesNotResolveIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => ReferenceResolver.Parse(UnknownKindTable));
    }

    /// <summary>
    /// A row answering for a way of differing that nothing declares is refused,
    /// because the property it does answer for is then left with no row.
    /// </summary>
    [Fact]
    public void ARowNamingAWayOfDifferingThatNothingDeclaresIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => ReferenceResolver.Parse(UnknownPropertyTable));
    }

    /// <summary>
    /// An answer outside the closed set is refused by name and never by number.
    /// </summary>
    [Fact]
    public void ARowGivingAnAnswerOutsideTheClosedSetIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => ReferenceResolver.Parse(UnknownAnswerTable));
    }

    /// <summary>
    /// A row that says what it decides and never why is refused.
    /// </summary>
    [Fact]
    public void ARowWithNoReasonIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => ReferenceResolver.Parse(UnarguedTable));
    }

    /// <summary>
    /// Two rows for one pair are refused. Whichever was read second would
    /// decide silently, and the one a reader found first would be the one they
    /// argued with.
    /// </summary>
    [Fact]
    public void TwoRowsForOnePairAreRefused()
    {
        Assert.Throws<InvalidOperationException>(() => ReferenceResolver.Parse(DoubleAnsweredTable));
    }

    /// <summary>
    /// A table leaving a pair unanswered is refused rather than defaulted. This
    /// is the check that makes adding a kind or a property a red suite instead
    /// of a silent answer nobody chose.
    /// </summary>
    [Fact]
    public void ATableLeavingAPairUnansweredIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => ReferenceResolver.Parse(IncompleteTable));
    }

    /// <summary>
    /// Renders every block the document carries, keyed by the name the document
    /// marks it with.
    /// </summary>
    private static Dictionary<string, string> Rendered()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rules"] = Table(
                "| Reference | A difference in | Answer | Reason |",
                "| --- | --- | --- | --- |",
                ReferenceResolver.Rules.Select(r => string.Format(
                    CultureInfo.InvariantCulture,
                    "| `{0}` | `{1}` | `{2}` | {3} |",
                    r.Kind,
                    r.Property,
                    r.Answer,
                    r.Reason))),
        };
    }

    private static string Table(string header, string rule, IEnumerable<string> rows)
    {
        var text = new StringBuilder(header).Append('\n').Append(rule);
        foreach (var row in rows)
        {
            text.Append('\n').Append(row);
        }

        return text.ToString();
    }

    private static string BlockIn(string text, string name)
    {
        var opening = "<!-- rendered from reference-comparison.json: " + name + " -->\n";
        var start = text.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, $"The document carries no block named '{name}'.");

        start += opening.Length;
        var end = text.IndexOf("\n<!-- end rendered -->", start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"The block named '{name}' is not closed.");

        return text[start..end];
    }

    private static ReferenceResolution Resolve(string kind, string incoming, IReadOnlyCollection<string> here)
    {
        return Enum.Parse<ReferenceKind>(Unquote(kind), ignoreCase: false) switch
        {
            ReferenceKind.Genre => ReferenceResolver.ResolveGenre(Unquote(incoming), here),
            ReferenceKind.Studio => ReferenceResolver.ResolveStudio(Unquote(incoming), here),
            ReferenceKind.Person => ReferenceResolver.ResolvePerson(Unquote(incoming), here),
            _ => throw new InvalidOperationException("The fixture names a kind with no entry point: " + kind),
        };
    }

    private static IReadOnlyList<string> Entries(string cell)
    {
        return Unquote(cell)
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static IReadOnlyList<string[]> TableRows(string header)
    {
        var lines = File.ReadAllLines(_document);
        var start = Array.FindIndex(lines, l => string.Equals(l.Trim(), header, StringComparison.Ordinal));
        Assert.True(start >= 0, "The document has no table headed: " + header);

        var rows = new List<string[]>();
        for (var i = start + 2; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('|'))
            {
                break;
            }

            rows.Add(line.Trim('|').Split('|').Select(c => c.Trim()).ToArray());
        }

        return rows;
    }

    // The document writes a value inside backticks so a reader can see where a
    // case about a space begins and ends. That is the whole reason a cell such
    // as `Blumhouse ` is readable at all.
    private static string Unquote(string cell) => cell.Trim().Trim('`');
}
