using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// What a decision becomes when it is written down, which is #48.
/// </summary>
/// <remarks>
/// Two things are held here. That a row carries every column the account needs,
/// for every outcome the rules can produce, because a column that is populated
/// on two outcomes out of three is a row an operator meets empty on the third.
/// And that a value too long to show is cut and says so, because a row showing
/// half a value without saying so is worse than one showing none of it.
/// <para>
/// The cases live in <c>docs/conflict-log.md</c> rather than in this file,
/// which is #70. A table of expectations written twice is a table that drifts,
/// and the copy the reader trusts is the document.
/// </para>
/// <para>
/// Nothing here is a log. No row goes through a pass, nothing keeps a row and
/// nothing shows one, and the document says so in the same words.
/// </para>
/// </remarks>
public class ConflictEntryTests
{
    private const string FixtureHeader = "| Case | The value | Characters shown | Truncated | The mistake it would catch |";

    /// <summary>
    /// The comment the rendered line sits under in the document.
    /// </summary>
    private const string RenderedLineOpens = "<!-- rendered from ShownValue.DisplayBound, read by ConflictEntryTests: edit the source, not this line -->";

    /// <summary>
    /// The comment that closes it.
    /// </summary>
    private const string RenderedLineCloses = "<!-- end of the rendered line -->";

    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "conflict-log.md");

    /// <summary>
    /// Every row of the fixture table in the document, as the document writes
    /// it. A row added there is a case here without anybody remembering to add
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
    /// The three outcomes a declared rule can produce, so every column below is
    /// asserted on each of them rather than on whichever one a case happened to
    /// use.
    /// </summary>
    /// <returns>The outcomes.</returns>
    public static TheoryData<ConflictOutcome> EveryOutcome()
    {
        var data = new TheoryData<ConflictOutcome>();

        foreach (var outcome in Enum.GetValues<ConflictOutcome>())
        {
            data.Add(outcome);
        }

        return data;
    }

    /// <summary>
    /// One case per row of the fixture table, with what the document says a row
    /// shows of the value and whether it says the value was cut.
    /// </summary>
    /// <param name="description">What the row is a case of.</param>
    /// <param name="value">The value, written as the document spells it.</param>
    /// <param name="shown">How many characters the document says are shown, or nothing at all.</param>
    /// <param name="truncated">Whether the document says the value was cut.</param>
    [Theory]
    [MemberData(nameof(FixtureRows))]
    public void AValueIsShownAsTheDocumentSaysItIs(string description, string value, string shown, string truncated)
    {
        Assert.False(string.IsNullOrWhiteSpace(description));

        var answer = ShownValue.Of(ValueFrom(value));

        Assert.Equal(CharactersShown(shown), answer.Text?.Length);
        Assert.Equal(Truth(truncated), answer.Truncated);
    }

    /// <summary>
    /// The number the document states is the number the source declares. The
    /// bound is a choice, and a document restating a choice is a second copy of
    /// it that goes on being read after the first one moves.
    /// </summary>
    [Fact]
    public void TheRenderedLineIsTheBoundTheSourceDeclares()
    {
        var expected = string.Format(
            CultureInfo.InvariantCulture,
            "A value longer than {0} characters is shown cut to {0}, and the row says it was cut.",
            ShownValue.DisplayBound);

        Assert.Equal(expected, RenderedLine());
    }

    /// <summary>
    /// The fixture table covers what this page is for. A table that lost these
    /// would still pass every row it kept, and the one it would lose first is
    /// the one nobody writes by hand: a value the bound falls inside a character
    /// of.
    /// </summary>
    [Fact]
    public void TheFixtureTableCoversTheAwkwardCases()
    {
        var descriptions = TableRows(FixtureHeader).Select(row => row[0]).ToList();

        Assert.Contains(descriptions, d => d.Contains("exactly the bound", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("one character longer than the bound", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("very long value", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("inside a character of", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("held nothing", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("no glyph", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every row names the mistake it would catch, and no two name the same one.
    /// A line copied from the row above is the shape a row takes when it was
    /// added for the count.
    /// </summary>
    [Fact]
    public void EveryRowNamesAMistakeAndNoTwoNameTheSameOne()
    {
        var mistakes = TableRows(FixtureHeader).Select(row => row[4]).ToList();

        Assert.NotEmpty(mistakes);
        Assert.All(mistakes, mistake => Assert.False(string.IsNullOrWhiteSpace(mistake)));
        Assert.Equal(mistakes.Count, mistakes.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The reader refuses a spelling it does not understand rather than reading
    /// it as text. A malformed piece taken literally is a short value that shows
    /// whole, so the row would stay green and stop being about anything.
    /// </summary>
    [Fact]
    public void TheReaderRefusesASpellingItDoesNotUnderstand()
    {
        Assert.ThrowsAny<Exception>(() => ValueFrom("`<repeat:A>`"));
        Assert.ThrowsAny<Exception>(() => ValueFrom("`<U+GGGG>`"));
        Assert.ThrowsAny<Exception>(() => ValueFrom("`<hexadecimal>`"));
        Assert.ThrowsAny<Exception>(() => ValueFrom("`A description`"));
        Assert.ThrowsAny<Exception>(() => ValueFrom("`\"unclosed`"));
    }

    /// <summary>
    /// Every column of an entry is populated, for every outcome a rule can
    /// produce. This is #48's first condition, and it is a theory over the
    /// outcomes rather than one case because a column filled in on two of the
    /// three is a row an operator meets empty on the third.
    /// </summary>
    /// <param name="outcome">The outcome the decision came back with.</param>
    [Theory]
    [MemberData(nameof(EveryOutcome))]
    public void EveryColumnIsPopulatedForEveryOutcome(ConflictOutcome outcome)
    {
        var item = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 8, 29, 4, 15, 0, TimeSpan.Zero);
        var plan = PlanOf(item, Decided("Overview", "Kept by hand", "From the peer", outcome, "item-locked-here"));

        var entry = Assert.Single(ConflictEntries.From(plan, at));

        Assert.Equal(item, entry.Item);
        Assert.Equal("Overview", entry.Field);
        Assert.Equal("Kept by hand", entry.LocalValue.Text);
        Assert.Equal("From the peer", entry.PeerValue.Text);
        Assert.Equal("item-locked-here", entry.Rule);
        Assert.Equal(outcome, entry.Outcome);
        Assert.Equal(plan.Direction, entry.Direction);
        Assert.Equal(at, entry.At);
    }

    /// <summary>
    /// The residual carries no rule and says so by carrying none, rather than by
    /// carrying a name nobody declared. That is the state the rule table is
    /// allowed to reach, and it is the row an operator most needs to be able to
    /// tell from a rule that chose to write nothing.
    /// </summary>
    [Fact]
    public void ARowWhereNoRuleFiredCarriesNoName()
    {
        var plan = PlanOf(Guid.NewGuid(), Decided("Overview", "Ours", "Theirs", ConflictOutcome.Refuse, rule: null));

        var entry = Assert.Single(ConflictEntries.From(plan, DateTimeOffset.UnixEpoch));

        Assert.Null(entry.Rule);
        Assert.Equal(ConflictOutcome.Refuse, entry.Outcome);
    }

    /// <summary>
    /// A field that never reached the conflict rules is not a conflict, so it
    /// owes no row. Every disposition below was settled before the two values
    /// were compared at all.
    /// </summary>
    /// <param name="disposition">The disposition the plan recorded.</param>
    [Theory]
    [InlineData(PlanDisposition.NotDeclared)]
    [InlineData(PlanDisposition.DoesNotMove)]
    [InlineData(PlanDisposition.OutsideTheKindGroup)]
    [InlineData(PlanDisposition.ExcludedByTheOperator)]
    public void AFieldThatNeverReachedTheRulesOwesNoRow(PlanDisposition disposition)
    {
        var change = new PlannedChange
        {
            Field = "Overview",
            LocalValue = "Ours",
            PeerValue = "Theirs",
            Disposition = disposition,
            Outcome = null,
            Rule = null,
            Writes = false,
        };

        Assert.False(ConflictEntries.IsOwed(change));
        Assert.Empty(ConflictEntries.From(PlanOf(Guid.NewGuid(), change), DateTimeOffset.UnixEpoch));
    }

    /// <summary>
    /// A row settled before the rules owes no entry even where something filled
    /// an outcome in on it. The disposition is the column that says whether the
    /// two values were ever compared, and a reading that took the outcome as
    /// that answer would put a register refusal into an account of
    /// disagreements.
    /// </summary>
    /// <remarks>
    /// This exists because the theory above could not have caught it. Every row
    /// there carries no outcome, so the condition that reads the disposition
    /// could be removed and the one beside it would go on refusing all four.
    /// Found by taking the disposition out and watching the suite stay green.
    /// </remarks>
    [Fact]
    public void ARowSettledBeforeTheRulesOwesNoRowEvenCarryingAnOutcome()
    {
        var change = new PlannedChange
        {
            Field = "Overview",
            LocalValue = "Ours",
            PeerValue = "Theirs",
            Disposition = PlanDisposition.DoesNotMove,
            Outcome = ConflictOutcome.KeepLocal,
            Rule = "values-agree",
            Writes = false,
        };

        Assert.False(ConflictEntries.IsOwed(change));
        Assert.Empty(ConflictEntries.From(PlanOf(Guid.NewGuid(), change), DateTimeOffset.UnixEpoch));
    }

    /// <summary>
    /// A row that says it reached the rules and carries no outcome is not a row
    /// this account can write, and it is refused rather than read for an outcome
    /// that is not there.
    /// </summary>
    [Fact]
    public void ARowThatReachedTheRulesWithNoOutcomeOwesNoRow()
    {
        var change = new PlannedChange
        {
            Field = "Overview",
            LocalValue = "Ours",
            PeerValue = "Theirs",
            Disposition = PlanDisposition.Decided,
            Outcome = null,
            Rule = null,
            Writes = false,
        };

        Assert.False(ConflictEntries.IsOwed(change));
        Assert.Empty(ConflictEntries.From(PlanOf(Guid.NewGuid(), change), DateTimeOffset.UnixEpoch));
    }

    /// <summary>
    /// A field both servers already agree on is a decision with nothing to tell.
    /// Without this the account is one row per field per item per pass, which is
    /// a log nobody opens twice.
    /// </summary>
    [Fact]
    public void AFieldBothServersAgreeOnOwesNoRow()
    {
        var change = Decided("Overview", "A description", "A description", ConflictOutcome.KeepLocal, "values-agree");

        Assert.False(ConflictEntries.IsOwed(change));
        Assert.Empty(ConflictEntries.From(PlanOf(Guid.NewGuid(), change), DateTimeOffset.UnixEpoch));
    }

    /// <summary>
    /// The first pass filling an empty field earns a row. It is the case most
    /// easily read as uneventful, and it is the one an operator asks about
    /// first: a value appeared in their library and they did not put it there.
    /// </summary>
    [Fact]
    public void AFirstPassFillingAnEmptyFieldEarnsARow()
    {
        var plan = PlanOf(
            Guid.NewGuid(),
            Decided("Overview", local: null, peer: "From the peer", ConflictOutcome.TakePeer, "local-value-absent"));

        var entry = Assert.Single(ConflictEntries.From(plan, DateTimeOffset.UnixEpoch));

        Assert.Null(entry.LocalValue.Text);
        Assert.False(entry.LocalValue.Truncated);
        Assert.Equal("From the peer", entry.PeerValue.Text);
    }

    /// <summary>
    /// The direction on a row is the one the plan was made under, rather than
    /// the one direction this plugin declares.
    /// </summary>
    /// <remarks>
    /// Every other case here is blind to the difference and always will be while
    /// the model has one member: a row reading the plan and a row writing
    /// `TwoWay` produce the same bytes, so a column quietly written as a
    /// constant would pass all of them. The plan is handed a value this plugin
    /// declares no name for, which no constant could produce, and the row is
    /// asked for it back.
    /// <para>
    /// The value is undeclared on purpose and it is not a configuration this
    /// plugin would accept. What refuses one of those is the validator, in
    /// `ADirectionThisPluginDoesNotDeclareIsRefused`, and it is a different
    /// question from whether a row carries what it was given. This is the fourth
    /// condition of #34.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheDirectionOnARowIsThePlansRatherThanTheOneDeclaredMember()
    {
        const SyncDirection NotDeclaredHere = (SyncDirection)7;

        var plan = new Plan { Direction = NotDeclaredHere };
        plan.Items.Add(ItemPlanOf(
            Guid.NewGuid(),
            Decided("Overview", "Ours", "Theirs", ConflictOutcome.Refuse, rule: null)));

        var entry = Assert.Single(ConflictEntries.From(plan, DateTimeOffset.UnixEpoch));

        Assert.Equal(NotDeclaredHere, entry.Direction);
        Assert.False(Enum.IsDefined(entry.Direction));
    }

    /// <summary>
    /// A pass stamps one moment across every row it produced, so the rows of one
    /// pass sort together instead of being spread across however long it ran.
    /// </summary>
    [Fact]
    public void EveryRowOfOnePassCarriesTheOneMomentItWasHandedIn()
    {
        var at = new DateTimeOffset(2026, 8, 29, 4, 15, 0, TimeSpan.Zero);
        var plan = PlanOf(
            Guid.NewGuid(),
            Decided("Overview", "Ours", "Theirs", ConflictOutcome.Refuse, rule: null),
            Decided("Tagline", "Ours", "Theirs", ConflictOutcome.Refuse, rule: null));

        var entries = ConflictEntries.From(plan, at);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Equal(at, entry.At));
    }

    /// <summary>
    /// A plan that decided nothing worth telling produces no rows at all, rather
    /// than one empty one.
    /// </summary>
    [Fact]
    public void APlanWithNothingToTellProducesNoRows()
    {
        Assert.Empty(ConflictEntries.From(new Plan(), DateTimeOffset.UnixEpoch));
    }

    /// <summary>
    /// A plan with two items keeps each row with the item it is about. The
    /// column exists so an operator can open the item, and a row carrying the
    /// wrong one sends them to somebody else's film.
    /// </summary>
    [Fact]
    public void ARowCarriesTheItemItIsAbout()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var plan = new Plan();
        plan.Items.Add(ItemPlanOf(first, Decided("Overview", "Ours", "Theirs", ConflictOutcome.Refuse, rule: null)));
        plan.Items.Add(ItemPlanOf(second, Decided("Tagline", "Ours", "Theirs", ConflictOutcome.Refuse, rule: null)));

        var entries = ConflictEntries.From(plan, DateTimeOffset.UnixEpoch);

        Assert.Equal(new[] { first, second }, entries.Select(entry => entry.Item));
    }

    /// <summary>
    /// A pass that hands over no plan at all is refused rather than answered
    /// with no rows, because those are the same answer from the caller's side
    /// and only one of them is true.
    /// </summary>
    [Fact]
    public void APassThatHandsOverNoPlanIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ConflictEntries.From(null!, DateTimeOffset.UnixEpoch));
    }

    /// <summary>
    /// A row that is not there is refused rather than read as a row owing
    /// nothing, for the same reason.
    /// </summary>
    [Fact]
    public void ARowThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ConflictEntries.IsOwed(null!));
    }

    private static PlannedChange Decided(string field, string? local, string? peer, ConflictOutcome outcome, string? rule) =>
        new()
        {
            Field = field,
            LocalValue = local,
            PeerValue = peer,
            Disposition = PlanDisposition.Decided,
            Outcome = outcome,
            Rule = rule,
            Writes = outcome == ConflictOutcome.TakePeer,
            ValueToWrite = outcome == ConflictOutcome.TakePeer ? peer : null,
        };

    private static Plan PlanOf(Guid item, params PlannedChange[] changes)
    {
        var plan = new Plan();
        plan.Items.Add(ItemPlanOf(item, changes));
        return plan;
    }

    private static ItemPlan ItemPlanOf(Guid item, params PlannedChange[] changes)
    {
        var itemPlan = new ItemPlan { LocalItemId = item, Kind = "Movie" };

        foreach (var change in changes)
        {
            itemPlan.Changes.Add(change);
        }

        return itemPlan;
    }

    /// <summary>
    /// The sentence the document renders from the declared bound, read out from
    /// between the two comments that mark it.
    /// </summary>
    private static string RenderedLine()
    {
        var lines = File.ReadAllLines(_document);

        var opens = Array.FindIndex(lines, line => string.Equals(line.Trim(), RenderedLineOpens, StringComparison.Ordinal));
        Assert.True(opens >= 0, "The document does not open a rendered line.");

        var closes = Array.FindIndex(lines, opens + 1, line => string.Equals(line.Trim(), RenderedLineCloses, StringComparison.Ordinal));
        Assert.True(closes > opens, "The document does not close the rendered line it opened.");

        var rendered = lines[(opens + 1)..closes]
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        return Assert.Single(rendered);
    }

    /// <summary>
    /// A value, built from the pieces the document spells it in. A piece the
    /// reader does not understand is refused rather than read as text.
    /// </summary>
    private static string? ValueFrom(string cell)
    {
        var spelling = cell.Trim().Trim('`');
        if (spelling.Length == 0)
        {
            return null;
        }

        var value = new StringBuilder();
        var at = 0;

        while (at < spelling.Length)
        {
            if (spelling[at] == '"')
            {
                var closes = spelling.IndexOf('"', at + 1);
                Assert.True(closes > at, "A quoted piece is not closed: " + cell);
                value.Append(spelling, at + 1, closes - at - 1);
                at = closes + 1;
            }
            else if (spelling[at] == '<')
            {
                var closes = spelling.IndexOf('>', at + 1);
                Assert.True(closes > at, "A named piece is not closed: " + cell);
                value.Append(Expand(spelling[(at + 1)..closes]));
                at = closes + 1;
            }
            else
            {
                Assert.Fail("A piece the reader does not understand: " + cell);
            }
        }

        return value.ToString();
    }

    private static string Expand(string piece)
    {
        if (piece.StartsWith("U+", StringComparison.Ordinal))
        {
            Assert.True(
                int.TryParse(piece[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codepoint),
                "A codepoint the reader cannot read: " + piece);

            return char.ConvertFromUtf32(codepoint);
        }

        if (piece.StartsWith("repeat:", StringComparison.Ordinal))
        {
            var parts = piece["repeat:".Length..].Split(':');
            Assert.True(parts.Length == 2, "A run the reader cannot read: " + piece);
            Assert.True(parts[0].Length == 1, "A run of more than one character: " + piece);

            return new string(parts[0][0], Length(parts[1]));
        }

        Assert.Fail("A named piece the reader does not understand: " + piece);
        return string.Empty;
    }

    /// <summary>
    /// A length, written either as a number or against the declared bound.
    /// </summary>
    private static int Length(string spelling)
    {
        if (spelling.StartsWith("bound", StringComparison.Ordinal))
        {
            var offset = spelling["bound".Length..];

            return offset.Length == 0
                ? ShownValue.DisplayBound
                : ShownValue.DisplayBound + int.Parse(offset, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        Assert.True(
            int.TryParse(spelling, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length),
            "A length the reader cannot read: " + spelling);

        return length;
    }

    /// <summary>
    /// How many characters the document says a row shows, or null where it says
    /// the row shows nothing at all.
    /// </summary>
    private static int? CharactersShown(string cell)
    {
        var spelling = cell.Trim().Trim('`');

        return spelling.Length == 0 ? null : Length(spelling);
    }

    private static bool Truth(string cell) => cell.Trim() switch
    {
        "yes" => true,
        "no" => false,
        _ => throw new FormatException("A yes or a no was expected and the document says: " + cell),
    };

    private static IReadOnlyList<string[]> TableRows(string header)
    {
        var lines = File.ReadAllLines(_document);
        var start = Array.FindIndex(lines, line => string.Equals(line.Trim(), header, StringComparison.Ordinal));
        Assert.True(start >= 0, "The document has no table headed: " + header);

        var rows = new List<string[]>();
        for (var i = start + 2; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('|'))
            {
                break;
            }

            rows.Add(line.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray());
        }

        return rows;
    }
}
