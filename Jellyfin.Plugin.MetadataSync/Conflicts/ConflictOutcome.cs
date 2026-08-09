namespace Jellyfin.Plugin.MetadataSync.Conflicts;

/// <summary>
/// What a conflict rule can produce. The set is closed, and it is closed for a
/// reason a fourth member would break: every outcome here either keeps a value
/// one of the two servers already holds or writes nothing at all.
/// </summary>
/// <remarks>
/// There is no merged outcome. A merged overview is a sentence neither operator
/// wrote, and a union of two genre sets is a library shape neither operator
/// chose. It is refused here rather than argued per field, so a rule cannot
/// reach for it by adding a row.
/// </remarks>
public enum ConflictOutcome
{
    /// <summary>
    /// This server's value stays and nothing is written. The peer is not told
    /// it lost, because on a symmetric pairing the peer is deciding the same
    /// field from its own side with the same rules.
    /// </summary>
    KeepLocal,

    /// <summary>
    /// The peer's value is written here, replacing this server's.
    /// </summary>
    TakePeer,

    /// <summary>
    /// Neither value is written and the difference is recorded for an operator.
    /// This is also what happens when no rule fires at all, which is #45.
    /// </summary>
    Refuse,
}
