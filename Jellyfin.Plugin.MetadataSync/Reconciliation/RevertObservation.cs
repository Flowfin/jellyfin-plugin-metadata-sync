using System;
using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// One item on this server as it stands now, for a revert to be decided
/// against.
/// </summary>
/// <remarks>
/// It is deliberately not an <see cref="ItemObservation"/>. A sync observation
/// carries the peer's copy of the item and what the peer holds in each field,
/// and a revert happens because there is no peer any more: the pairing is over,
/// nothing is asked of the other server, and the only two things a revert
/// compares are what the library holds now and what this plugin recorded
/// writing.
/// <para>
/// Reading the library into one of these is the caller's, exactly as it is for a
/// pass. What arrives here is data, so a revert can be arranged in a test by
/// writing one down with nothing running.
/// </para>
/// </remarks>
public sealed class RevertObservation
{
    /// <summary>
    /// Gets the item on this server.
    /// </summary>
    public Guid LocalItemId { get; init; }

    /// <summary>
    /// Gets the kind of item, as the server names it.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets when this server last saved the item, as the write path compares it.
    /// </summary>
    /// <remarks>
    /// It travels for the reason it travels on a plan: the write path refuses an
    /// item that something else has written since it was read, and an item
    /// carrying no stamp is refused rather than written blind.
    /// </remarks>
    public string? LastSavedHere { get; init; }

    /// <summary>
    /// Gets what the item holds now, field by field.
    /// </summary>
    /// <remarks>
    /// Every field the caller read, not only the ones this plugin wrote. A
    /// revert that was handed only the fields with a record could not count the
    /// fields without one, and that count is #66's second condition rather than
    /// a nicety.
    /// </remarks>
    public Collection<RevertField> Fields { get; } = new();
}
