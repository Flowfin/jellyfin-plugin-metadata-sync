namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// What resolving one parent-plus-ordinal item established. The set is closed,
/// and every member other than <see cref="Resolved"/> is a reason an item is
/// written into the unmatched register rather than a failure to report.
/// </summary>
/// <remarks>
/// There is no member meaning the nearest ordinal was taken. Every case this
/// enumeration names is a case where some published matcher picks a neighbour,
/// and a neighbour picked here writes one episode's overview onto another with
/// nothing in the result saying a guess was made.
/// <para>
/// The set is closed rather than open because the decision is written as an
/// ordered chain of checks, and a chain is where a matcher quietly becomes
/// wrong: the case that was handled and the case that fell through look the same
/// afterwards. Naming every arm is what makes them look different, and the suite
/// refuses a member with no fixture row so a new arm cannot arrive unexercised.
/// </para>
/// </remarks>
public enum OrdinalVerdict
{
    /// <summary>
    /// The parent resolved and exactly one item under it carries the same
    /// season and episode number. This is the only member that names a match.
    /// </summary>
    Resolved,

    /// <summary>
    /// No candidate's parent is the same work as this item's parent. The
    /// episode was never looked at, because an ordinal means nothing until the
    /// series it counts within is known.
    /// </summary>
    ParentDidNotResolve,

    /// <summary>
    /// The item carries neither a season and episode pair nor an absolute
    /// number, so it has no ordinal to be resolved by and no identifiers of its
    /// own answered either.
    /// </summary>
    NotNumbered,

    /// <summary>
    /// The item is numbered absolutely and carries no season and episode pair.
    /// An absolute number counts through a series as one provider divided it
    /// into seasons, so converting it needs the season lengths that provider
    /// used, which is exactly what differs between two libraries built from
    /// different providers.
    /// </summary>
    AbsoluteNumbering,

    /// <summary>
    /// The item's ordinal is a range rather than a number, which is what a file
    /// holding more than one episode carries. It is not one episode, so there is
    /// no one episode on the peer it is the same as.
    /// </summary>
    CoversMoreThanOneEpisode,

    /// <summary>
    /// The item is in season zero, which is the server's bucket for everything a
    /// provider did not place in a numbered season. The position of a special
    /// inside that bucket is assigned by whichever provider each server used, so
    /// two servers agreeing on the number is not evidence they mean one episode.
    /// </summary>
    SeasonZero,

    /// <summary>
    /// The parent resolved and nothing under it carries this ordinal. Whatever
    /// is nearest to it is not consulted.
    /// </summary>
    NothingAtThatOrdinal,

    /// <summary>
    /// The parent resolved and more than one item under it carries this ordinal.
    /// Which of them a value would be written against is not decidable from the
    /// numbering, so nothing is written.
    /// </summary>
    OrdinalHeldTwice,
}
