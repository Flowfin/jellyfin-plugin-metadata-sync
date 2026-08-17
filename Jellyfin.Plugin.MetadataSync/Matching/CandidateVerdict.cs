namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// What asking a set of candidates which of them a work is established.
/// </summary>
/// <remarks>
/// Four members and no fifth meaning the question was passed over. Every set of
/// candidates lands on one of these, so a caller cannot receive an answer that
/// means nothing was decided and reads as though something was.
/// </remarks>
public enum CandidateVerdict
{
    /// <summary>
    /// Exactly one candidate is the same work, on the identifier comparison this
    /// plugin already declares.
    /// </summary>
    Resolved,

    /// <summary>
    /// No candidate was offered at all. Nothing on this side carries any of the
    /// identifiers, which is a library that has not been scanned with the same
    /// provider rather than a disagreement about a work.
    /// </summary>
    NothingOffered,

    /// <summary>
    /// Candidates were offered and none of them is the same work. Either no
    /// provider is present on both sides, or one that is present names a
    /// different work, and both are answered by the comparison rather than here.
    /// </summary>
    NoneIsTheSameWork,

    /// <summary>
    /// More than one candidate is the same work, which is a film held in two
    /// cuts or two qualities and is not an error. Nothing in the identifiers
    /// separates them, so nothing is written and both are reported.
    /// </summary>
    HeldByMoreThanOne,
}
