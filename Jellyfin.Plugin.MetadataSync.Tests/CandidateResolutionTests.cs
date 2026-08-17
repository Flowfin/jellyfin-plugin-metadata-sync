using System;
using System.Collections.Generic;
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
/// transport in any of it, so a run needs nothing but the assembly.
/// </para>
/// </remarks>
public class CandidateResolutionTests
{
    /// <summary>
    /// The two items one work is held by in the ambiguity below, in the order
    /// the answer reports them, which is by identity rather than by arrival.
    /// </summary>
    private static readonly string[] _bothCuts = { "here:extended", "here:theatrical" };

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
