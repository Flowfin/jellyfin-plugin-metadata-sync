using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The declared conflict rules, held up against the document that publishes
/// them and against the fixture table that argues them.
/// </summary>
/// <remarks>
/// What this file holds is the design and never the behaviour: that the
/// document is the rule table, that every rule is argued by at least one case,
/// and that every case expects the outcome its rule declares. Running those
/// cases against something is <see cref="ConflictResolverTests"/>, which reads
/// the same rows through the same reader. The split is worth keeping, because a
/// failure here says the table and the document disagree and a failure there
/// says the plugin and the table do.
/// </remarks>
public class ConflictRuleTests
{
    /// <summary>
    /// A rule table whose rule produces an outcome the closed set does not
    /// carry. Used by <see cref="RefusalTests"/> to reach that refusal.
    /// </summary>
    internal const string UndeclaredOutcomeRules = """
        { "rules": [ { "id": "newest-wins", "condition": "The peer's value is newer.", "outcome": "TakeTheNewer", "reason": "A clock both servers agree on is not a thing this plan has." } ] }
        """;

    /// <summary>
    /// A rule table whose rule says when it fires and never why it is right.
    /// Used by <see cref="RefusalTests"/> to reach that refusal.
    /// </summary>
    internal const string UnarguedRules = """
        { "rules": [ { "id": "values-agree", "condition": "The two values are equal.", "outcome": "KeepLocal", "reason": "" } ] }
        """;

    /// <summary>
    /// A rule table carrying one name twice. Used by <see cref="RefusalTests"/>
    /// to reach that refusal.
    /// </summary>
    internal const string DuplicateNameRules = """
        { "rules": [
            { "id": "values-agree", "condition": "The two values are equal.", "outcome": "KeepLocal", "reason": "There is nothing to decide." },
            { "id": "values-agree", "condition": "Neither side has a value.", "outcome": "KeepLocal", "reason": "There is nothing to decide here either." } ] }
        """;

    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "conflicts.md");

    private static readonly string[] _lockSpellings = ["none", "item here", "field here", "field on the peer"];

    private static readonly string[] _outcomeNames = ["KeepLocal", "TakePeer", "Refuse"];

    /// <summary>
    /// The table that ships inside the assembly loads, and loads in the order
    /// it is written in. This is the neighbour every refusal below differs
    /// from by one thing.
    /// </summary>
    [Fact]
    public void TheRuleTableThatShipsInTheAssemblyLoads()
    {
        Assert.NotEmpty(ConflictRules.Rules);
        Assert.Equal("item-locked-here", ConflictRules.Rules[0].Id);
        Assert.NotNull(ConflictRules.Find("peer-field-locked"));
        Assert.Null(ConflictRules.Find("newest-wins"));
    }

    /// <summary>
    /// A rule table that is not embedded under the name asked for is refused
    /// rather than read as an empty rule set. An empty rule set refuses every
    /// difference, which looks safe and is silently a plugin that does nothing.
    /// </summary>
    [Fact]
    public void ARuleTableThatIsNotEmbeddedIsRefused()
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => ConflictRules.Load("Jellyfin.Plugin.MetadataSync.Conflicts.no-such-rules.json"));

        Assert.Contains("no-such-rules.json", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Text that describes no rule set at all is refused.
    /// </summary>
    [Fact]
    public void RuleTextThatDescribesNoRuleSetIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => ConflictRules.Parse("null"));
    }

    /// <summary>
    /// A rule producing an outcome the closed set does not carry is refused
    /// when the table is read, by name and never by number.
    /// </summary>
    /// <remarks>
    /// The fixture is the outcome this plan actually rejected. Resolving on the
    /// newer timestamp is decision 2 in #1 and it was refused there, so a table
    /// reintroducing it should not be readable at all.
    /// </remarks>
    [Fact]
    public void ARuleProducingAnOutcomeNothingDeclaresIsRefused()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => ConflictRules.Parse(UndeclaredOutcomeRules));

        Assert.Contains("TakeTheNewer", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A rule that says when it fires and never why it is right is refused. The
    /// reason column is the whole point of declaring the rules before the
    /// resolver, so a blank one is a rule nobody can disagree with.
    /// </summary>
    [Fact]
    public void ARuleWithNoReasonIsRefused()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => ConflictRules.Parse(UnarguedRules));

        Assert.Contains("values-agree", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two rules under one name are refused. A rule is reported to the operator
    /// by its name, and a name held by two rules names neither of them.
    /// </summary>
    [Fact]
    public void TwoRulesUnderOneNameAreRefused()
    {
        Assert.Throws<InvalidOperationException>(() => ConflictRules.Parse(DuplicateNameRules));
    }

    /// <summary>
    /// The outcome set is closed and it is these three. A fourth member is what
    /// a merged value or a union would arrive as, and it would arrive without
    /// anybody editing this document, so the set is asserted here by name.
    /// </summary>
    [Fact]
    public void TheOutcomeSetIsClosedAndNamed()
    {
        Assert.Equal(_outcomeNames, Enum.GetNames<ConflictOutcome>());
    }

    /// <summary>
    /// The committed document is exactly what the source produces. Both
    /// directions: a rule added to the table with no rendering fails, and a
    /// line edited in the document that the source does not produce fails too.
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
    /// Every block the source produces is in the document. A rendering nobody
    /// pasted in would otherwise pass the comparison above by never being
    /// compared.
    /// </summary>
    [Fact]
    public void EveryRenderedBlockIsPresentAndNoOtherIs()
    {
        var text = File.ReadAllText(_document).Replace("\r\n", "\n", StringComparison.Ordinal);

        var inDocument = text.Split('\n')
            .Where(l => l.StartsWith("<!-- rendered from the declared rules: ", StringComparison.Ordinal))
            .Select(l => l["<!-- rendered from the declared rules: ".Length..].Replace(" -->", string.Empty, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Rendered().Keys.Order(StringComparer.Ordinal).ToList(), inDocument);
    }

    /// <summary>
    /// Every declared rule is argued by at least one case. A rule with no
    /// fixture is a rule nobody has had to think about twice, and it is the one
    /// that turns out to be wrong.
    /// </summary>
    [Fact]
    public void EveryRuleIsArguedByAtLeastOneCase()
    {
        var named = Fixtures().Select(f => f[5]).ToHashSet(StringComparer.Ordinal);

        var unargued = ConflictRules.Rules
            .Select(r => r.Id)
            .Where(id => !named.Contains("`" + id + "`"))
            .ToList();

        Assert.Empty(unargued);
    }

    /// <summary>
    /// Every case names a rule the table declares and expects the outcome that
    /// rule declares. A case is not free to expect something its own rule does
    /// not produce, which is the drift a document and a fixture table always
    /// develop when nothing holds them together.
    /// </summary>
    [Fact]
    public void EveryCaseExpectsWhatItsRuleProduces()
    {
        foreach (var fixture in Fixtures())
        {
            var expected = Unquote(fixture[6]);
            Assert.Contains(expected, Enum.GetNames<ConflictOutcome>(), StringComparer.Ordinal);

            if (fixture[5].Length == 0)
            {
                continue;
            }

            var rule = ConflictRules.Find(Unquote(fixture[5]));
            Assert.NotNull(rule);
            Assert.Equal(rule.Outcome.ToString(), expected);
        }
    }

    /// <summary>
    /// A case that names no rule expects a refusal. This is the fail-closed
    /// floor from #45, held by the suite rather than by the sentence in the
    /// document that states it.
    /// </summary>
    [Fact]
    public void ACaseThatNoRuleAnswersExpectsARefusal()
    {
        var residual = Fixtures().Where(f => f[5].Length == 0).ToList();

        Assert.NotEmpty(residual);
        Assert.All(residual, f => Assert.Equal("Refuse", Unquote(f[6])));
    }

    /// <summary>
    /// The locks column uses the four spellings the document declares. A fifth
    /// spelling is a case nobody can construct, and it reads exactly like one
    /// that was thought about.
    /// </summary>
    [Fact]
    public void EveryCaseNamesLocksInOneOfTheDeclaredSpellings()
    {
        Assert.All(Fixtures(), f => Assert.Contains(f[4], _lockSpellings, StringComparer.Ordinal));
    }

    /// <summary>
    /// The fixture table keeps its near misses. A table that lost them would
    /// still pass every row it kept, and the rows it kept are the easy ones.
    /// </summary>
    [Fact]
    public void TheFixtureTableKeepsTheCasesThatDecideTheOrder()
    {
        var cases = Fixtures().Select(f => f[0]).ToList();

        Assert.Contains(cases, c => c.Contains("locked here and the values already agree", StringComparison.Ordinal));
        Assert.Contains(cases, c => c.Contains("by one character", StringComparison.Ordinal));
        Assert.Contains(cases, c => c.Contains("locked and this server has nothing", StringComparison.Ordinal));
        Assert.Contains(cases, c => c.Contains("nothing and this server holds what this plugin wrote", StringComparison.Ordinal));
        Assert.Contains(cases, c => c.Contains("whitespace only", StringComparison.Ordinal));
        Assert.Contains(cases, c => c.Contains("neither side has a value", StringComparison.Ordinal));
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
                "| Order | Rule | When it fires | Outcome | Reason |",
                "| --- | --- | --- | --- | --- |",
                ConflictRules.Rules.Select((r, i) => string.Format(
                    CultureInfo.InvariantCulture,
                    "| {0} | `{1}` | {2} | `{3}` | {4} |",
                    i + 1,
                    r.Id,
                    r.Condition,
                    r.Outcome,
                    r.Reason))),

            ["outcomes"] = Table(
                "| Outcome | Rules that produce it |",
                "| --- | --- |",
                Enum.GetValues<ConflictOutcome>().Select(o => string.Format(
                    CultureInfo.InvariantCulture,
                    "| `{0}` | {1} |",
                    o,
                    Producers(o)))),
        };
    }

    /// <summary>
    /// The rules producing one outcome, as the document writes them. An outcome
    /// no rule produces renders as such rather than as an empty cell, because
    /// an empty cell reads as an oversight.
    /// </summary>
    private static string Producers(ConflictOutcome outcome)
    {
        var producers = ConflictRules.Rules
            .Where(r => r.Outcome == outcome)
            .Select(r => "`" + r.Id + "`")
            .ToList();

        return producers.Count == 0 ? "no rule, and the residual alone" : string.Join(", ", producers);
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

    /// <summary>
    /// Reads back the block the document marks with a name, so a failure
    /// compares one table against one table.
    /// </summary>
    private static string BlockIn(string text, string name)
    {
        var opening = "<!-- rendered from the declared rules: " + name + " -->\n";
        var start = text.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, $"The document carries no block named '{name}'.");

        start += opening.Length;
        var end = text.IndexOf("\n<!-- end rendered -->", start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"The block named '{name}' is not closed.");

        return text[start..end];
    }

    // The fixture table is read by ConflictFixtures, which is also what hands
    // the rows to the resolver. One reader rather than two: a second parser
    // here would agree with that one until the day the table changed shape, and
    // then one of the two files would still be green.
    private static IReadOnlyList<string[]> Fixtures() => ConflictFixtures.Rows();

    // The document writes a value, a rule and an outcome inside backticks, so a
    // reader can see where a value that is only whitespace begins and ends.
    private static string Unquote(string cell) => ConflictFixtures.Unquote(cell);
}
