using System.Collections.Generic;

namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// What asking one set of candidates established, the candidates behind it, and
/// the sentence a register entry is written from.
/// </summary>
/// <remarks>
/// The matching candidates are carried rather than only counted, because the
/// case this type exists for is the one an operator has to act on: two items
/// here that are one work, which they resolve by merging the items or by saying
/// which of them this plugin writes to. A count tells them a problem exists and
/// not which items it is about.
/// </remarks>
public sealed class CandidateResolution
{
    internal CandidateResolution(CandidateVerdict verdict, IReadOnlyList<Candidate> sameWork, int offered, string reason)
    {
        Verdict = verdict;
        SameWork = sameWork;
        Offered = offered;
        Reason = reason;
    }

    /// <summary>
    /// Gets what was established.
    /// </summary>
    public CandidateVerdict Verdict { get; }

    /// <summary>
    /// Gets the candidate this work is, which is set only where the verdict is
    /// <see cref="CandidateVerdict.Resolved"/> and is null otherwise. It is null
    /// for an ambiguity as well, because an ambiguity has no answer and reading
    /// one out of it is the guess this refuses.
    /// </summary>
    public Candidate? Match => Verdict == CandidateVerdict.Resolved ? SameWork[0] : null;

    /// <summary>
    /// Gets every candidate the comparison called the same work, ordered by
    /// identity rather than by arrival. It holds one entry for a resolution,
    /// more than one for an ambiguity, and none otherwise.
    /// </summary>
    public IReadOnlyList<Candidate> SameWork { get; }

    /// <summary>
    /// Gets how many candidates were offered, which is what separates a library
    /// that returned nothing from one that returned rows none of which is this
    /// work.
    /// </summary>
    public int Offered { get; }

    /// <summary>
    /// Gets the sentence this outcome is reported by, naming the numbers it was
    /// decided from so a register entry says what was compared rather than only
    /// that it failed.
    /// </summary>
    public string Reason { get; }
}
