namespace Jellyfin.Plugin.MetadataSync.References;

/// <summary>
/// What the table may say about a difference. The set is closed at two, and the
/// missing third member is the point.
/// </summary>
/// <remarks>
/// There is no answer meaning "different". A difference the table does not fold
/// already makes two values two values, so a row saying so would change
/// nothing, and a reader meeting it would reasonably expect it to do something.
/// </remarks>
public enum ReferenceAnswer
{
    /// <summary>
    /// Two values differing only in this are one thing, and the incoming
    /// reference resolves to the one already here.
    /// </summary>
    Same,

    /// <summary>
    /// Two values differing only in this are neither joined nor kept apart by
    /// this plugin. The pair is reported and an operator decides.
    /// </summary>
    Undecided,
}
