using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Matching;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// What happens when more than one item carries the identifiers of one work,
/// which is #31.
/// </summary>
/// <remarks>
/// The case is ordinary rather than broken. A film held in two cuts, or in two
/// qualities, is one work and two items, and an operator who keeps both did
/// nothing wrong. What would be wrong is answering anyway, and the three ways of
/// answering anyway each have a leg here: taking the first, taking all of them,
/// and letting the answer move when the rows arrive in a different order.
/// <para>
/// Everything below is constructed. There is no library, no clock and no
/// transport in any of it, so a run needs nothing but the assembly. Nothing here
/// was copied out of anybody's library, and the awkward cases are built from the
/// spellings the identifier table already declares rules for.
/// </para>
/// <para>
/// The cases live in `docs/matching.md` rather than in this file, which is #70.
/// A table of expectations written twice is a table that drifts, and the copy
/// the reader trusts is the document. What is left here as a literal is the
/// arrangement no table can carry: an absent dictionary, a candidate with no
/// identity, and the twenty four orderings of one set.
/// </para>
/// </remarks>
public class CandidateResolutionTests
{
    private const string FixtureHeader = "| Case | The work | Offered here | Outcome | The mistake it would catch |";

    /// <summary>
    /// The two items one work is held by in the ambiguity below, in the order
    /// the answer reports them, which is by identity rather than by arrival.
    /// </summary>
    private static readonly string[] _bothCuts = { "here:extended", "here:theatrical" };

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
    /// from the document a reader is expected to trust.
    /// </summary>
    /// <param name="description">What the row is a case of.</param>
    /// <param name="work">The identifiers of the work being placed.</param>
    /// <param name="offered">The candidates this server offered for it.</param>
    /// <param name="expected">The outcome the document states.</param>
    [Theory]
    [MemberData(nameof(FixtureRows))]
    public void TheResolverAnswersWhatTheDocumentSaysItAnswers(string description, string work, string offered, string expected)
    {
        Assert.False(string.IsNullOrWhiteSpace(description));

        var answer = CandidateResolver.Resolve(IdentifiersFrom(work), CandidatesFrom(offered));

        Assert.Equal(Enum.Parse<CandidateVerdict>(Unquote(expected), ignoreCase: false), answer.Verdict);
    }

    /// <summary>
    /// The set of answers is closed, and a member with no row is a verdict
    /// nothing runs. It is the leg that catches an arm added to the chain later,
    /// which otherwise falls through into whichever arm follows it and is read
    /// by no case here.
    /// </summary>
    [Fact]
    public void EveryVerdictTheResolverCanGiveHasAFixtureRow()
    {
        var covered = TableRows(FixtureHeader).Select(r => Unquote(r[3])).ToHashSet(StringComparer.Ordinal);

        var unrun = Enum.GetNames<CandidateVerdict>()
            .Where(name => !covered.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(unrun);

        // The other direction. A cell holding a verdict this plugin does not
        // declare would be parsed nowhere and read as a row nobody wrote.
        Assert.All(covered, name => Assert.Contains(name, Enum.GetNames<CandidateVerdict>(), StringComparer.Ordinal));
    }

    /// <summary>
    /// The fixture table covers the awkward cases this table exists for. A table
    /// that lost them would still pass every row it kept, and the one it would
    /// lose first is the one an operator meets: two items here that are one work.
    /// </summary>
    [Fact]
    public void TheFixtureTableCoversTheAwkwardCases()
    {
        var descriptions = TableRows(FixtureHeader).Select(r => r[0]).ToList();

        Assert.Contains(descriptions, d => d.Contains("two cuts of the same film", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("provider name in lower case", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("zero-padded", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("no candidate at all", StringComparison.Ordinal));
        Assert.Contains(descriptions, d => d.Contains("no identifiers of its own", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every fixture row names the mistake it would catch. A row that names none
    /// is a row nobody argued for, and once the suite is green it cannot be told
    /// apart from one that could never have failed.
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
    /// lands and never what the mistake is, and the outcome column beside it
    /// already carries that.
    /// </summary>
    [Fact]
    public void AFixtureRowNamingAnOutcomeRatherThanAMistakeIsRefused()
    {
        foreach (var row in TableRows(FixtureHeader))
        {
            Assert.DoesNotContain(Unquote(row[4]), Enum.GetNames<CandidateVerdict>(), StringComparer.Ordinal);
            Assert.Contains(' ', row[4]);
        }
    }

    /// <summary>
    /// No two rows name the same mistake. A line copied from the row above is the
    /// shape a row takes when it was added for the count rather than for the
    /// mistake, and it is the one failure this column cannot report about itself.
    /// </summary>
    [Fact]
    public void NoTwoFixtureRowsNameTheSameMistake()
    {
        var mistakes = TableRows(FixtureHeader).Select(r => r[4]).ToList();

        Assert.Equal(mistakes.Count, mistakes.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// A candidate cell this reader does not understand is refused rather than
    /// read as something. A malformed candidate read as an identity carrying no
    /// identifiers still lands on a refusing verdict, so the row would stay green
    /// and stop being about anything.
    /// </summary>
    [Fact]
    public void ACandidateCellInASpellingThisReaderDoesNotUnderstandIsRefused()
    {
        Assert.ThrowsAny<Exception>(() => CandidatesFrom("here:1 Tmdb=550"));
        Assert.ThrowsAny<Exception>(() => CandidatesFrom("here:1[Tmdb=550"));
        Assert.ThrowsAny<Exception>(() => CandidatesFrom("[Tmdb=550]"));
    }

    /// <summary>
    /// The ordinary resolution, which is the neighbour every refusal below is
    /// one change away from.
    /// </summary>
    [Fact]
    public void ExactlyOneCandidateNamingTheWorkResolves()
    {
        var answer = CandidateResolver.Resolve(
            Identifiers(("Tmdb", "603")),
            new[]
            {
                Row("here:1", ("Tmdb", "603")),
                Row("here:2", ("Tmdb", "604")),
            });

        Assert.Equal(CandidateVerdict.Resolved, answer.Verdict);
        Assert.NotNull(answer.Match);
        Assert.Equal("here:1", answer.Match!.Id);
        Assert.Equal(2, answer.Offered);
        Assert.Contains("here:1", answer.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// The whole issue in one leg. Two items are the same work, nothing in the
    /// identifiers separates them, and the answer is that it did not resolve.
    /// </summary>
    [Fact]
    public void TwoCandidatesNamingOneWorkAreAmbiguousRatherThanAChoice()
    {
        var answer = CandidateResolver.Resolve(
            Identifiers(("Tmdb", "603")),
            new[]
            {
                Row("here:theatrical", ("Tmdb", "603")),
                Row("here:extended", ("Tmdb", "603")),
            });

        Assert.Equal(CandidateVerdict.HeldByMoreThanOne, answer.Verdict);
        Assert.Null(answer.Match);
        Assert.Equal(2, answer.SameWork.Count);
    }

    /// <summary>
    /// An ambiguity an operator cannot act on is a refusal that reads as a
    /// failure, so the answer carries the items rather than a count of them.
    /// </summary>
    [Fact]
    public void AnAmbiguityNamesEveryCandidateBehindIt()
    {
        var answer = CandidateResolver.Resolve(
            Identifiers(("Tmdb", "603")),
            new[]
            {
                Row("here:theatrical", ("Tmdb", "603")),
                Row("here:extended", ("Tmdb", "603")),
                Row("here:other-film", ("Tmdb", "604")),
            });

        Assert.Equal(_bothCuts, answer.SameWork.Select(c => c.Id));

        Assert.Contains("here:extended", answer.Reason, StringComparison.Ordinal);
        Assert.Contains("here:theatrical", answer.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// The second condition of #31. A library returns rows in whatever order its
    /// query planner chose, and that order changes after a rescan, so an answer
    /// that moves with it writes to a different item for no reason anybody could
    /// see.
    /// </summary>
    /// <remarks>
    /// Every permutation of a four-row set is run, and the whole answer is
    /// compared rather than only the verdict. A verdict that held while the
    /// reported items or the sentence moved would still put a different pair of
    /// item names in front of an operator on two runs over one library.
    /// </remarks>
    [Fact]
    public void TheAnswerDoesNotDependOnTheOrderTheCandidatesArriveIn()
    {
        var rows = new[]
        {
            Row("here:theatrical", ("Tmdb", "603")),
            Row("here:extended", ("Tmdb", "603")),
            Row("here:different-film", ("Tmdb", "604")),
            Row("here:nothing-in-common", ("Imdb", "tt0133093")),
        };

        var answers = Permutations(rows)
            .Select(order => CandidateResolver.Resolve(Identifiers(("Tmdb", "603")), order))
            .Select(a => new
            {
                a.Verdict,
                Match = a.Match?.Id,
                SameWork = string.Join("|", a.SameWork.Select(c => c.Id)),
                a.Offered,
                a.Reason,
            })
            .Distinct()
            .ToList();

        Assert.Equal(24, Permutations(rows).Count);
        Assert.Single(answers);
        Assert.Equal(CandidateVerdict.HeldByMoreThanOne, answers[0].Verdict);
    }

    /// <summary>
    /// The fourth condition of #31. One set of candidates is read the same way
    /// whichever side offered it, because there is one function and not two.
    /// </summary>
    /// <remarks>
    /// The bound is worth stating rather than leaving to be read into this. No
    /// pairing contract is referenced by this repository, so nothing here has
    /// ever been told by a peer that its side was ambiguous. What this asserts is
    /// that a set carrying the peer's own spelling of its identities lands on the
    /// same verdict, the same ordering and the same shape of sentence, so the day
    /// such a set does arrive there is no second answer waiting for it.
    /// </remarks>
    [Fact]
    public void ASetOfferedFromTheOtherSideIsAnsweredByTheSameCall()
    {
        var here = CandidateResolver.Resolve(
            Identifiers(("Tmdb", "603")),
            new[]
            {
                Row("a", ("Tmdb", "603")),
                Row("b", ("Tmdb", "603")),
            });

        var there = CandidateResolver.Resolve(
            Identifiers(("Tmdb", "603")),
            new[]
            {
                Row("b", ("Tmdb", "603")),
                Row("a", ("Tmdb", "603")),
            });

        Assert.Equal(here.Verdict, there.Verdict);
        Assert.Equal(here.SameWork.Select(c => c.Id), there.SameWork.Select(c => c.Id));
        Assert.Equal(here.Reason, there.Reason, StringComparer.Ordinal);
    }

    /// <summary>
    /// Two libraries that never shared a provider and two libraries that did and
    /// disagree are different work for an operator, so they are different
    /// answers rather than one absence.
    /// </summary>
    [Fact]
    public void NoCandidateOfferedIsNotTheSameAsNoCandidateMatching()
    {
        var nothingOffered = CandidateResolver.Resolve(
            Identifiers(("Tmdb", "603")),
            Array.Empty<Candidate>());

        var nothingMatching = CandidateResolver.Resolve(
            Identifiers(("Tmdb", "603")),
            new[] { Row("here:1", ("Tmdb", "604")) });

        Assert.Equal(CandidateVerdict.NothingOffered, nothingOffered.Verdict);
        Assert.Equal(CandidateVerdict.NoneIsTheSameWork, nothingMatching.Verdict);
        Assert.NotEqual(nothingOffered.Reason, nothingMatching.Reason);
        Assert.Empty(nothingOffered.SameWork);
        Assert.Empty(nothingMatching.SameWork);
    }

    /// <summary>
    /// Being the only row a query returned is not evidence of anything, and it
    /// is the shape a matcher reaches for when it wants to answer.
    /// </summary>
    [Fact]
    public void TheOnlyCandidateOfferedIsNotTakenForBeingTheOnlyOne()
    {
        var answer = CandidateResolver.Resolve(
            Identifiers(("Tmdb", "603")),
            new[] { Row("here:1", ("Imdb", "tt0133093")) });

        Assert.Equal(CandidateVerdict.NoneIsTheSameWork, answer.Verdict);
        Assert.Null(answer.Match);
    }

    /// <summary>
    /// A work this server holds no identifiers for resolves nothing, and it is
    /// the neighbour of the refusal that the dictionary is absent altogether.
    /// </summary>
    [Fact]
    public void AWorkWithNoIdentifiersOfItsOwnResolvesNothing()
    {
        var answer = CandidateResolver.Resolve(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new[] { Row("here:1", ("Tmdb", "603")) });

        Assert.Equal(CandidateVerdict.NoneIsTheSameWork, answer.Verdict);
    }

    /// <summary>
    /// An absent dictionary is a caller defect rather than a work with nothing
    /// on it, and the two are told apart here rather than both resolving to
    /// nothing.
    /// </summary>
    [Fact]
    public void ResolvingAgainstIdentifiersThatAreNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(
            () => CandidateResolver.Resolve(null!, Array.Empty<Candidate>()));
    }

    /// <summary>
    /// An absent candidate set is a caller defect rather than a library that
    /// returned nothing, and answering it as the latter would report a library
    /// state nobody measured.
    /// </summary>
    [Fact]
    public void ResolvingCandidatesThatAreNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(
            () => CandidateResolver.Resolve(Identifiers(("Tmdb", "603")), null!));
    }

    /// <summary>
    /// An ambiguity is only useful if the items behind it can be named, so a
    /// candidate with no identity is refused where it is made.
    /// </summary>
    [Fact]
    public void ACandidateWithNoIdentityIsRefused()
    {
        Assert.Throws<ArgumentException>(
            () => new Candidate(" ", Identifiers(("Tmdb", "603"))));
    }

    /// <summary>
    /// A candidate with no identifier dictionary at all is refused where it is
    /// made rather than being compared as one carrying nothing.
    /// </summary>
    [Fact]
    public void ACandidateWithNoIdentifiersIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new Candidate("here:1", null!));
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

    /// <summary>
    /// The candidates a cell offers, written as an identity followed by that
    /// candidate's own identifiers in brackets, separated by a space.
    /// </summary>
    private static IReadOnlyCollection<Candidate> CandidatesFrom(string written)
    {
        var candidates = new List<Candidate>();
        var cell = Unquote(written);

        var at = 0;
        while (at < cell.Length)
        {
            if (cell[at] == ' ')
            {
                at++;
                continue;
            }

            var opens = cell.IndexOf('[', at);
            var closes = opens < 0 ? -1 : cell.IndexOf(']', opens);

            // A cell in a spelling this reader was not written for is refused
            // here rather than passed on. Read leniently, `here:1 Tmdb=550` is an
            // identity carrying nothing, which refuses for a reason that has
            // nothing to do with the row.
            Assert.True(opens > at, "A fixture candidate is not written id[Provider=Value]: " + cell);
            Assert.True(closes > opens, "A fixture candidate is not written id[Provider=Value]: " + cell);

            candidates.Add(new Candidate(cell[at..opens], IdentifiersFrom(cell[(opens + 1)..closes])));
            at = closes + 1;
        }

        return candidates;
    }

    /// <summary>
    /// The identifiers a cell holds, written the way `provider-identifiers.md`
    /// writes them, so one spelling covers both documents.
    /// </summary>
    private static IReadOnlyDictionary<string, string> IdentifiersFrom(string written)
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

    // The document writes the identifiers and the outcome inside backticks, so a
    // reader can see where a cell begins and ends.
    private static string Unquote(string cell) => cell.Trim().Trim('`');

    private static Candidate Row(string id, params (string Provider, string Value)[] identifiers) =>
        new(id, Identifiers(identifiers));

    private static Dictionary<string, string> Identifiers(params (string Provider, string Value)[] identifiers)
    {
        var read = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (provider, value) in identifiers)
        {
            read[provider] = value;
        }

        return read;
    }

    private static List<Candidate[]> Permutations(IReadOnlyList<Candidate> rows)
    {
        var found = new List<Candidate[]>();
        Permute(rows.ToArray(), 0, found);
        return found;
    }

    private static void Permute(Candidate[] rows, int from, List<Candidate[]> found)
    {
        if (from == rows.Length - 1)
        {
            found.Add(rows.ToArray());
            return;
        }

        for (var i = from; i < rows.Length; i++)
        {
            (rows[from], rows[i]) = (rows[i], rows[from]);
            Permute(rows, from + 1, found);
            (rows[from], rows[i]) = (rows[i], rows[from]);
        }
    }
}
