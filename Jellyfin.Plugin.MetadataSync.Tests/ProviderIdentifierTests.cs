using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Matching;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The provider identifier comparison, held up against the document that
/// declares it.
/// </summary>
/// <remarks>
/// The fixture rows are read out of `docs/provider-identifiers.md` rather than
/// restated here. A table of expectations written twice is a table that drifts,
/// and the copy the reader trusts is the document.
/// </remarks>
public class ProviderIdentifierTests
{
    private const string FixtureHeader = "| Case | This server | The peer | Outcome | The mistake it would catch |";

    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "provider-identifiers.md");

    /// <summary>
    /// Every row of the fixture table in the document, as the document writes
    /// it. A row added there is a test here without anybody remembering to add
    /// one, which is the direction that fails closed.
    /// </summary>
    /// <returns>The rows.</returns>
    public static TheoryData<string, string, string, string> FixtureRows()
    {
        var data = new TheoryData<string, string, string, string>();

        foreach (var row in TableRows(FixtureHeader))
        {
            data.Add(row[0], row[1], row[2], row[3]);
        }

        return data;
    }

    /// <summary>
    /// One test per row of the fixture table, with its expected outcome quoted
    /// from the document that a reader is expected to trust.
    /// </summary>
    /// <param name="description">What the row is a case of.</param>
    /// <param name="local">This server's identifiers.</param>
    /// <param name="peer">The peer's identifiers.</param>
    /// <param name="expected">The outcome the document states.</param>
    [Theory]
    [MemberData(nameof(FixtureRows))]
    public void TheComparisonAnswersWhatTheDocumentSaysItAnswers(string description, string local, string peer, string expected)
    {
        Assert.False(string.IsNullOrWhiteSpace(description));

        var verdict = ProviderIdentifiers.Compare(Identifiers(local), Identifiers(peer));

        Assert.Equal(Enum.Parse<ProviderIdentifierVerdict>(Unquote(expected), ignoreCase: false), verdict);
    }

    /// <summary>
    /// The fixture table covers the cases the rules are about. A table that lost
    /// its awkward rows would still pass every row it kept.
    /// </summary>
    [Fact]
    public void TheFixtureTableCoversEveryCaseTheRulesAreAbout()
    {
        var descriptions = TableRows(FixtureHeader).Select(r => r[0]).ToList();

        Assert.Contains(descriptions, d => d.Contains("clean single-provider match", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("agreement across two providers", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("disagreement across two providers", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("differing only in case", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("differing only in whitespace", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("empty dictionary", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every fixture row names the mistake it would catch. A row that names none
    /// is a row nobody argued for, and once the suite is green it cannot be told
    /// apart from one that could never have failed.
    /// </summary>
    /// <remarks>
    /// It is a cell per row rather than a paragraph beside the table, because a
    /// paragraph covers the rows somebody remembered to mention and the rows it
    /// leaves out are the ones nobody argued for.
    /// </remarks>
    [Fact]
    public void EveryFixtureRowNamesAMistake()
    {
        var rows = TableRows(FixtureHeader);

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(row[4].Length > 40, row[0]));
    }

    /// <summary>
    /// A cell holding an outcome name rather than a sentence says where a
    /// mistake lands and never what the mistake is, and the outcome column two
    /// cells to the left already carries it.
    /// </summary>
    [Fact]
    public void AFixtureRowNamingAnOutcomeRatherThanAMistakeIsRefused()
    {
        foreach (var row in TableRows(FixtureHeader))
        {
            Assert.DoesNotContain(Unquote(row[4]), Enum.GetNames<ProviderIdentifierVerdict>(), StringComparer.Ordinal);
            Assert.Contains(' ', row[4]);
        }
    }

    /// <summary>
    /// No two rows name the same mistake. A line copied from the row above is
    /// the shape a row takes when it was added for the count rather than for the
    /// mistake, and it is the one failure this column cannot report about itself.
    /// </summary>
    [Fact]
    public void NoTwoFixtureRowsNameTheSameMistake()
    {
        var mistakes = TableRows(FixtureHeader).Select(r => r[4]).ToList();

        Assert.Equal(mistakes.Count, mistakes.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The rules table in the document is a rendering of the file that ships
    /// inside the assembly. A rendering that has drifted is worse than none,
    /// because it is the copy somebody argues from.
    /// </summary>
    [Fact]
    public void TheDocumentSaysExactlyWhatTheTableSays()
    {
        var text = File.ReadAllText(_document);

        foreach (var rule in ProviderIdentifiers.Rules)
        {
            Assert.Contains(RuleLine("`" + rule.Provider + "`", rule), text, StringComparison.Ordinal);
        }

        Assert.Contains(RuleLine("_" + ProviderIdentifiers.DefaultRule.Provider + "_", ProviderIdentifiers.DefaultRule), text, StringComparison.Ordinal);

        var lines = TableRows("| Provider | Identifier comparison | Whitespace | Normalisation | Reason |").Count;
        Assert.Equal(ProviderIdentifiers.Rules.Count + 1, lines);
    }

    /// <summary>
    /// The page names the type that decides what a match is worth where two
    /// local items carry one identifier, and that type is one this plugin
    /// declares.
    ///
    /// It said <c>#31</c> instead until tonight, five days after the type
    /// landed, so the file where the comparison is argued handed a reader an
    /// open issue for a question the tree beside it answers.
    ///
    /// What this reads is the name and nothing else. Whether the sentence around
    /// it describes the type correctly is a judgement no reading of this tree
    /// makes, and it is what the review is for; what it catches is a rename that
    /// leaves the page pointing at nothing, which is the way this pointer would
    /// go wrong without anybody editing the page.
    ///
    /// The name is taken from the type rather than written here. A literal in
    /// this file would be a second declaration of it, and the leg would then go
    /// on passing against a page and a literal that agreed with each other after
    /// the type had been renamed out from under both.
    /// </summary>
    [Fact]
    public void ThePageNamesTheTypeThatDecidesWhatAMatchIsWorth()
    {
        Assert.Contains("`" + nameof(CandidateResolver) + "`", File.ReadAllText(_document), StringComparison.Ordinal);
    }

    /// <summary>
    /// Every rule is argued. A row with no reason is a rule nobody can disagree
    /// with later, which is how a matcher acquires a normalisation nobody meant.
    /// </summary>
    [Fact]
    public void EveryRuleCarriesAReason()
    {
        Assert.NotEmpty(ProviderIdentifiers.Rules);
        Assert.All(ProviderIdentifiers.Rules, r => Assert.True(r.Reason.Length > 40, r.Provider));
        Assert.True(ProviderIdentifiers.DefaultRule.Reason.Length > 40);
    }

    /// <summary>
    /// A provider the table does not name falls to the default rule rather than
    /// to whatever the first row happens to say.
    /// </summary>
    [Fact]
    public void AProviderWithNoRowFallsToTheDefaultRule()
    {
        Assert.Equal(ProviderIdentifiers.DefaultRule.Provider, ProviderIdentifiers.RuleFor("Anidb").Provider);
        Assert.Equal("Tmdb", ProviderIdentifiers.RuleFor("tmdb").Provider);
    }

    /// <summary>
    /// The ordinary case, and the neighbour the two refusals below differ from
    /// by one thing.
    /// </summary>
    [Fact]
    public void TwoDictionariesThatAreThereAreCompared()
    {
        var verdict = ProviderIdentifiers.Compare(Identifiers("Tmdb=550"), Identifiers("Tmdb=550"));

        Assert.Equal(ProviderIdentifierVerdict.Match, verdict);
    }

    /// <summary>
    /// There is nothing on this side to compare. Reading a missing dictionary as
    /// an empty one would answer `NoBasis`, which is a real answer to a question
    /// that was never asked.
    /// </summary>
    [Fact]
    public void ComparingWithNoLocalDictionaryIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ProviderIdentifiers.Compare(null!, Identifiers("Tmdb=550")));
    }

    /// <summary>
    /// The same failure on the other side.
    /// </summary>
    [Fact]
    public void ComparingWithNoPeerDictionaryIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ProviderIdentifiers.Compare(Identifiers("Tmdb=550"), null!));
    }

    /// <summary>
    /// The table that ships inside the assembly is the one the comparison runs
    /// on. This is the neighbour for the two loader refusals below.
    /// </summary>
    [Fact]
    public void TheTableThatShipsInTheAssemblyLoads()
    {
        Assert.NotEmpty(ProviderIdentifiers.Load(ProviderIdentifiers.EmbeddedResourceName).Rules);
    }

    /// <summary>
    /// A build that did not embed the table declares no rule. Reading that as an
    /// empty table would silently put every provider on the default rule.
    /// </summary>
    [Fact]
    public void ATableThatIsNotEmbeddedIsRefused()
    {
        Assert.Throws<InvalidOperationException>(
            () => ProviderIdentifiers.Load("Jellyfin.Plugin.MetadataSync.Matching.no-such-table.json"));
    }

    /// <summary>
    /// Table text that describes no table is refused rather than read as one
    /// with no rows.
    /// </summary>
    [Fact]
    public void TableTextThatDescribesNoTableIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => ProviderIdentifiers.Parse("null"));
    }

    private static string RuleLine(string name, ProviderIdentifierRule rule) => string.Format(
        CultureInfo.InvariantCulture,
        "| {0} | {1} | {2} | {3} | {4} |",
        name,
        rule.ValueComparison,
        rule.Trimmed ? "trimmed" : "not trimmed",
        rule.Normalisation,
        rule.Reason);

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

            var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            rows.Add(cells);
        }

        return rows;
    }

    private static IReadOnlyDictionary<string, string> Identifiers(string written)
    {
        var identifiers = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in Unquote(written).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = pair.IndexOf('=', StringComparison.Ordinal);
            Assert.True(split > 0, "A fixture identifier is not written Provider=Value: " + pair);
            identifiers[pair[..split]] = pair[(split + 1)..];
        }

        return identifiers;
    }

    // The document writes an identifier and an outcome inside backticks, so a
    // reader can see where a value with surrounding whitespace begins and ends.
    // That is the whole reason a cell such as `Tmdb= 550 ` is readable at all.
    private static string Unquote(string cell) => cell.Trim().Trim('`');
}
