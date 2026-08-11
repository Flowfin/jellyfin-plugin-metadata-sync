using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Matching;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The two-step resolution of an item identified by its parent and an ordinal,
/// held up against the document that declares it.
/// </summary>
/// <remarks>
/// The fixture rows are read out of `docs/matching.md` rather than restated
/// here, for the reason the provider identifier suite gives: a table of
/// expectations written twice is a table that drifts, and the copy the reader
/// trusts is the document.
/// <para>
/// Most of what this suite is for is the refusals. A matcher that resolves the
/// clean case is easy and every published one manages it; what separates them is
/// which of the awkward cases they answer anyway.
/// </para>
/// </remarks>
public class OrdinalResolutionTests
{
    private const string FixtureHeader = "| Case | This server | The peer | Outcome | The mistake it would catch |";

    private const string VerdictHeader = "| Verdict | Step | What it says |";

    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "matching.md");

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
    /// from the document a reader is expected to trust. The step is asserted
    /// beside it, so a verdict that arrived under the wrong step is caught here
    /// rather than in the register that would have carried it.
    /// </summary>
    /// <param name="description">What the row is a case of.</param>
    /// <param name="here">The item on this server.</param>
    /// <param name="there">The candidates the peer offered.</param>
    /// <param name="expected">The outcome the document states.</param>
    [Theory]
    [MemberData(nameof(FixtureRows))]
    public void TheResolverAnswersWhatTheDocumentSaysItAnswers(string description, string here, string there, string expected)
    {
        Assert.False(string.IsNullOrWhiteSpace(description));

        var verdict = Enum.Parse<OrdinalVerdict>(Unquote(expected), ignoreCase: false);
        var resolution = OrdinalResolver.Resolve(Item(here), Candidates(there));

        Assert.Equal(verdict, resolution.Verdict);
        Assert.Equal(OrdinalResolver.StepFor(verdict), resolution.Step);
        Assert.Equal(verdict == OrdinalVerdict.Resolved, resolution.Match is not null);
        Assert.False(string.IsNullOrWhiteSpace(resolution.Reason));
    }

    /// <summary>
    /// Every answer the resolver can give is exercised by a row. An arm added to
    /// the chain without a row would be a case that falls through into whichever
    /// arm follows it, and nothing about the result would say so.
    /// </summary>
    [Fact]
    public void EveryVerdictHasAFixtureRow()
    {
        var exercised = TableRows(FixtureHeader).Select(r => Unquote(r[3])).ToList();

        Assert.All(
            Enum.GetNames<OrdinalVerdict>(),
            name => Assert.Contains(name, exercised, StringComparer.Ordinal));
    }

    /// <summary>
    /// The table of answers in the document is a rendering of the sentences
    /// declared in the plugin. A rendering that has drifted is worse than none,
    /// because it is the copy somebody argues from.
    /// </summary>
    [Fact]
    public void TheDocumentSaysExactlyWhatTheResolverSays()
    {
        var text = File.ReadAllText(_document);

        foreach (var verdict in Enum.GetValues<OrdinalVerdict>())
        {
            Assert.Contains(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "| `{0}` | `{1}` | {2} |",
                    verdict,
                    OrdinalResolver.StepFor(verdict),
                    OrdinalResolver.Statement(verdict)),
                text,
                StringComparison.Ordinal);
        }

        Assert.Equal(Enum.GetValues<OrdinalVerdict>().Length, TableRows(VerdictHeader).Count);
    }

    /// <summary>
    /// A verdict added with no declared sentence is refused rather than rendered
    /// as an empty cell in the document and an empty reason in the register.
    /// </summary>
    [Fact]
    public void AVerdictWithNoDeclaredSentenceIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrdinalResolver.Statement((OrdinalVerdict)99));
    }

    /// <summary>
    /// Every fixture row names the mistake it would catch. A row that names none
    /// is a row nobody argued for, and it cannot be told from one that could not
    /// have failed.
    /// </summary>
    [Fact]
    public void EveryFixtureRowNamesAMistake()
    {
        var rows = TableRows(FixtureHeader);

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(row[4].Length > 40, row[0]));
    }

    /// <summary>
    /// A cell holding a verdict name rather than a sentence says where a mistake
    /// lands and never what the mistake is.
    /// </summary>
    [Fact]
    public void AFixtureRowNamingAVerdictRatherThanAMistakeIsRefused()
    {
        foreach (var row in TableRows(FixtureHeader))
        {
            Assert.DoesNotContain(Unquote(row[4]), Enum.GetNames<OrdinalVerdict>(), StringComparer.Ordinal);
            Assert.Contains(' ', row[4]);
        }
    }

    /// <summary>
    /// No two rows name the same mistake. A line copied from the row above is
    /// how a table grows rows that add a count and no cover.
    /// </summary>
    [Fact]
    public void NoTwoFixtureRowsNameTheSameMistake()
    {
        var mistakes = TableRows(FixtureHeader).Select(r => r[4]).ToList();

        Assert.Equal(mistakes.Count, mistakes.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The whole of season one except the wanted episode, so the nearest number
    /// above and the nearest number below are both present and neither is taken.
    /// This is the near miss the whole refusal exists for.
    /// </summary>
    [Fact]
    public void NothingResolvesByProximityOfNumbering()
    {
        var peer = new List<OrdinalIdentity>();

        for (var number = 1; number <= 12; number++)
        {
            if (number != 5)
            {
                peer.Add(Episode(1, number));
            }
        }

        var resolution = OrdinalResolver.Resolve(Episode(1, 5), peer);

        Assert.Equal(OrdinalVerdict.NothingAtThatOrdinal, resolution.Verdict);
        Assert.Null(resolution.Match);
    }

    /// <summary>
    /// One item left unmatched under a series that resolved. Taking it reads as
    /// arithmetic and is the fallback easiest to write by accident.
    /// </summary>
    [Fact]
    public void TheOnlyRemainingCandidateIsNotTaken()
    {
        var resolution = OrdinalResolver.Resolve(Episode(1, 5), new[] { Episode(4, 11) });

        Assert.Equal(OrdinalVerdict.NothingAtThatOrdinal, resolution.Verdict);
    }

    /// <summary>
    /// A peer file covering episodes five and six is not the same item as this
    /// server's episode five, in either direction. Containment is proximity
    /// wearing an equality sign.
    /// </summary>
    [Fact]
    public void ARangeThatContainsTheNumberIsNotTheItemThatCarriesIt()
    {
        var range = new OrdinalIdentity(Identifiers("Tvdb=121361"), 1, 5, 6, null);

        Assert.Equal(OrdinalVerdict.NothingAtThatOrdinal, OrdinalResolver.Resolve(Episode(1, 5), new[] { range }).Verdict);
        Assert.Equal(OrdinalVerdict.CoversMoreThanOneEpisode, OrdinalResolver.Resolve(range, new[] { Episode(1, 5) }).Verdict);
    }

    /// <summary>
    /// The candidates are narrowed by parent first. A resolver that narrowed by
    /// ordinal first would answer from whichever series was offered first, and
    /// this asserts the answer does not move when that order changes.
    /// </summary>
    [Fact]
    public void TheParentDecidesBeforeTheOrdinalIsRead()
    {
        var wanted = Episode(1, 5);
        var elsewhere = new OrdinalIdentity(Identifiers("Tvdb=305288"), 1, 5, null, null);

        var first = OrdinalResolver.Resolve(wanted, new[] { elsewhere, Episode(1, 5) });
        var second = OrdinalResolver.Resolve(wanted, new[] { Episode(1, 5), elsewhere });

        Assert.Equal(OrdinalVerdict.Resolved, first.Verdict);
        Assert.Equal(OrdinalVerdict.Resolved, second.Verdict);
        Assert.Equal(OrdinalStep.Ordinal, first.Step);
    }

    /// <summary>
    /// The register's whole reason for carrying the step: a series that did not
    /// resolve is one thing to fix, and an episode that did not resolve inside a
    /// series that did is another. Only one verdict belongs to the first step.
    /// </summary>
    [Fact]
    public void OnlyAParentThatDidNotResolveIsTheParentStep()
    {
        foreach (var verdict in Enum.GetValues<OrdinalVerdict>())
        {
            Assert.Equal(
                verdict == OrdinalVerdict.ParentDidNotResolve ? OrdinalStep.Parent : OrdinalStep.Ordinal,
                OrdinalResolver.StepFor(verdict));
        }
    }

    /// <summary>
    /// A failure at either step names the ordinal it was asked about, so a
    /// register entry says what was compared rather than only that it failed.
    /// </summary>
    [Fact]
    public void AFailureAtTheOrdinalStepNamesTheOrdinal()
    {
        var resolution = OrdinalResolver.Resolve(Episode(2, 7), new[] { Episode(2, 8) });

        Assert.Contains("S02E07", resolution.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// The spellings the document writes are the spellings the reason carries,
    /// including the two that are not a season and a number at all.
    /// </summary>
    [Fact]
    public void AnOrdinalIsSpelledTheWayTheDocumentSpellsIt()
    {
        Assert.Equal("S01E05", OrdinalResolver.Spelled(Episode(1, 5)));
        Assert.Equal("S01E05-E06", OrdinalResolver.Spelled(new OrdinalIdentity(Identifiers("Tvdb=121361"), 1, 5, 6, null)));
        Assert.Equal("absolute 137", OrdinalResolver.Spelled(new OrdinalIdentity(Identifiers("Tvdb=121361"), null, null, null, 137)));
        Assert.Equal("no numbering", OrdinalResolver.Spelled(new OrdinalIdentity(Identifiers("Tvdb=121361"), null, null, null, null)));
    }

    /// <summary>
    /// A season with no number inside it is not a numbering this plugin resolves
    /// on, and neither is a number with no season. Reading either as complete is
    /// how an episode acquires a position nobody stated.
    /// </summary>
    [Fact]
    public void HalfOfANumberingIsNotANumbering()
    {
        var seasonOnly = new OrdinalIdentity(Identifiers("Tvdb=121361"), 1, null, null, null);
        var numberOnly = new OrdinalIdentity(Identifiers("Tvdb=121361"), null, 5, null, null);

        Assert.Equal(OrdinalVerdict.NotNumbered, OrdinalResolver.Resolve(seasonOnly, new[] { Episode(1, 5) }).Verdict);
        Assert.Equal(OrdinalVerdict.NotNumbered, OrdinalResolver.Resolve(numberOnly, new[] { Episode(1, 5) }).Verdict);
    }

    /// <summary>
    /// Season zero is a season the server uses and no season is a different
    /// state. Collapsing the two would send a special down the arm written for
    /// an item that carries no numbering.
    /// </summary>
    [Fact]
    public void SeasonZeroIsNotTheSameAsNoSeason()
    {
        var special = new OrdinalIdentity(Identifiers("Tvdb=121361"), 0, 2, null, null);
        var unnumbered = new OrdinalIdentity(Identifiers("Tvdb=121361"), null, 2, null, null);

        Assert.Equal(OrdinalVerdict.SeasonZero, OrdinalResolver.Resolve(special, new[] { special }).Verdict);
        Assert.Equal(OrdinalVerdict.NotNumbered, OrdinalResolver.Resolve(unnumbered, new[] { special }).Verdict);
    }

    /// <summary>
    /// There is nothing on this side to resolve. Reading a missing item as an
    /// empty one would answer a question that was never asked.
    /// </summary>
    [Fact]
    public void ResolvingWithNoItemIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => OrdinalResolver.Resolve(null!, Array.Empty<OrdinalIdentity>()));
    }

    /// <summary>
    /// The same failure on the other side. A peer that offered no candidate list
    /// at all is not a peer that offered an empty one.
    /// </summary>
    [Fact]
    public void ResolvingWithNoCandidatesIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => OrdinalResolver.Resolve(Episode(1, 5), null!));
    }

    /// <summary>
    /// An identity with no parent identifiers at all is refused where it is
    /// built, rather than resolving against every series at once later.
    /// </summary>
    [Fact]
    public void AnIdentityWithNoParentIdentifierDictionaryIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new OrdinalIdentity(null!, 1, 5, null, null));
    }

    /// <summary>
    /// Spelling something that is not there is refused for the same reason.
    /// </summary>
    [Fact]
    public void SpellingNoItemIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => OrdinalResolver.Spelled(null!));
    }

    private static OrdinalIdentity Episode(int season, int number) =>
        new(Identifiers("Tvdb=121361"), season, number, null, null);

    private static IReadOnlyCollection<OrdinalIdentity> Candidates(string written)
    {
        var candidates = new List<OrdinalIdentity>();

        foreach (var candidate in Unquote(written).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            candidates.Add(Item(candidate));
        }

        return candidates;
    }

    private static OrdinalIdentity Item(string written)
    {
        var text = Unquote(written).Trim();
        var split = text.LastIndexOf(' ');
        Assert.True(split > 0, "A fixture item is not written as identifiers, a space, then an ordinal: " + text);

        var ordinal = Ordinal(text[(split + 1)..]);

        return new OrdinalIdentity(Identifiers(text[..split]), ordinal.Season, ordinal.Number, ordinal.Last, ordinal.Absolute);
    }

    // The document spells an ordinal the way a person writes one, so the parsing
    // is here rather than the numbers being written out in four columns nobody
    // would read.
    private static (int? Season, int? Number, int? Last, int? Absolute) Ordinal(string written)
    {
        if (string.Equals(written, "-", StringComparison.Ordinal))
        {
            return (null, null, null, null);
        }

        if (written[0] == 'A')
        {
            return (null, null, null, Number(written[1..]));
        }

        Assert.StartsWith("S", written, StringComparison.Ordinal);

        var episode = written.IndexOf('E', StringComparison.Ordinal);
        Assert.True(episode > 1, "A fixture ordinal names a season and no episode: " + written);

        var season = Number(written[1..episode]);
        var rest = written[(episode + 1)..];
        var range = rest.IndexOf("-E", StringComparison.Ordinal);

        return range < 0
            ? (season, Number(rest), null, null)
            : (season, Number(rest[..range]), Number(rest[(range + 2)..]), null);
    }

    private static int Number(string written) => int.Parse(written, CultureInfo.InvariantCulture);

    private static IReadOnlyDictionary<string, string> Identifiers(string written)
    {
        var identifiers = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.Equals(written, "-", StringComparison.Ordinal))
        {
            return identifiers;
        }

        foreach (var pair in written.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = pair.IndexOf('=', StringComparison.Ordinal);
            Assert.True(split > 0, "A fixture identifier is not written Provider=Value: " + pair);
            identifiers[pair[..split].Trim()] = pair[(split + 1)..].Trim();
        }

        return identifiers;
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

    // The document writes an item and an outcome inside backticks, so a reader
    // can see where a cell that is only a dash begins and ends.
    private static string Unquote(string cell) => cell.Trim().Trim('`');
}
