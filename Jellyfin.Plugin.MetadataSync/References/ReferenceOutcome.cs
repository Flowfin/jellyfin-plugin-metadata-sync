namespace Jellyfin.Plugin.MetadataSync.References;

/// <summary>
/// What resolving one incoming reference can produce. The set is closed, and
/// every member is something a reader of the plan can be shown.
/// </summary>
/// <remarks>
/// There is no member meaning the reference was dropped. Dropping one silently
/// is the failure this whole resolution exists against: an operator reads a
/// sync that reported success beside a cast list missing two names.
/// </remarks>
public enum ReferenceOutcome
{
    /// <summary>
    /// Exactly one entry already here is the same reference under the table,
    /// and it is named.
    /// </summary>
    Resolved,

    /// <summary>
    /// Nothing here is the same and nothing here is close, so the reference
    /// would be created. What marks a created entry as this plugin's is #47,
    /// and until that exists nothing is created from this outcome.
    /// </summary>
    Create,

    /// <summary>
    /// Something here is close in a way the table refuses to decide, or more
    /// than one entry here is the same reference. Both are reported with the
    /// entries that caused them.
    /// </summary>
    Undecided,

    /// <summary>
    /// The incoming value is not a reference at all: it is empty, or it is
    /// nothing but space. It is refused with that reason rather than skipped.
    /// </summary>
    Refused,
}
