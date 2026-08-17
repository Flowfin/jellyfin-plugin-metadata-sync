using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// Answers which of a set of candidates a work is, and refuses to answer where
/// more than one of them is that work.
/// </summary>
/// <remarks>
/// Two items on one server can legitimately carry one provider identifier. A
/// film held in two cuts, or in two qualities, is one work and two items, and
/// neither is an error. When a change arrives for that work there are two
/// candidates and nothing in the identifiers separating them, so this answers
/// that it did not resolve.
/// <para>
/// What it refuses is the three ways of answering anyway. Taking the first
/// candidate makes the answer depend on the order a library returned its rows
/// in, so the same pass over the same data writes to a different item after a
/// rescan. Taking all of them makes one edit into two, and where the two cuts
/// differ in a field, one of them is now wrong. Taking the only remaining
/// candidate after some other rule has thinned the set is the same guess with a
/// step in front of it.
/// </para>
/// <para>
/// This adds no comparison of its own. Whether two items are the same work is
/// <see cref="ProviderIdentifiers.Compare"/>, which is the table this plugin
/// already declares, and every candidate here is decided by it. What is decided
/// here is what to do when that comparison says yes more than once, and that is
/// a question about one server's own duplicates, which the other server cannot
/// see and no resolver on it could answer.
/// </para>
/// <para>
/// This is pure. It reads the identifiers it is handed and nothing else - no
/// library, no clock, no file, no transport - so every case is decidable from a
/// table with nothing running.
/// </para>
/// </remarks>
public static class CandidateResolver
{
    /// <summary>
    /// Resolves one work against the candidates offered for it.
    /// </summary>
    /// <param name="identifiers">The provider identifiers of the work being placed.</param>
    /// <param name="candidates">The candidates offered, which need not have been filtered to matches already.</param>
    /// <returns>What was established, and the candidates behind it.</returns>
    /// <remarks>
    /// The candidates are not required to be filtered down first. Handing in a
    /// set somebody else narrowed would move the comparison out of here, and the
    /// statement that nothing resolved by being the only one left would then be
    /// made about a set this never saw.
    /// <para>
    /// The set is read the same way whichever side offered it. An ambiguity the
    /// peer reports is that side's candidates handed in here, and it lands on the
    /// same verdict through the same lines, so the two cannot drift into two
    /// answers.
    /// </para>
    /// </remarks>
    public static CandidateResolution Resolve(
        IReadOnlyDictionary<string, string> identifiers,
        IReadOnlyCollection<Candidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(candidates);

        var sameWork = new List<Candidate>();

        foreach (var candidate in candidates)
        {
            if (ProviderIdentifiers.Compare(candidate.Identifiers, identifiers) == ProviderIdentifierVerdict.Match)
            {
                sameWork.Add(candidate);
            }
        }

        // Ordered by identity rather than left in arrival order, so what comes
        // back is the same object whichever order the rows arrived in. The
        // verdict alone would already be order-independent; the reported set and
        // the sentence would not, and those are what an operator reads.
        sameWork.Sort(static (left, right) => string.CompareOrdinal(left.Id, right.Id));

        if (sameWork.Count > 1)
        {
            return new CandidateResolution(
                CandidateVerdict.HeldByMoreThanOne,
                sameWork,
                candidates.Count,
                HeldByMoreThanOne(sameWork));
        }

        if (sameWork.Count == 1)
        {
            return new CandidateResolution(
                CandidateVerdict.Resolved,
                sameWork,
                candidates.Count,
                Resolved(sameWork[0], candidates.Count));
        }

        // Nothing offered and nothing matching are separated because they are
        // different work for an operator. The first is a library that has never
        // been scanned with a provider the other side uses; the second is two
        // libraries that were, and that disagree or share no provider.
        return candidates.Count == 0
            ? new CandidateResolution(CandidateVerdict.NothingOffered, sameWork, 0, NothingOffered())
            : new CandidateResolution(CandidateVerdict.NoneIsTheSameWork, sameWork, candidates.Count, NoneIsTheSameWork(candidates.Count));
    }

    private static string HeldByMoreThanOne(IReadOnlyList<Candidate> sameWork) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} candidates carry provider identifiers naming this same work: {1}. Nothing in the identifiers separates them, so which one a value would be written to is not decidable and nothing is written.",
        sameWork.Count,
        string.Join(", ", sameWork.Select(c => c.Id)));

    private static string Resolved(Candidate match, int offered) => string.Format(
        CultureInfo.InvariantCulture,
        "Exactly one of the {0} candidate(s) offered carries provider identifiers naming this same work, which is {1}.",
        offered,
        match.Id);

    private static string NothingOffered() =>
        "No candidate was offered, so nothing here carries any identifier this work is named by. That is a library built without the providers the other side used rather than a disagreement about a work.";

    private static string NoneIsTheSameWork(int offered) => string.Format(
        CultureInfo.InvariantCulture,
        "None of the {0} candidate(s) offered carries provider identifiers naming this same work. Either no provider is present on both sides, or one that is names a different work, and neither is settled by taking the nearest.",
        offered);
}
