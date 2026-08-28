using System;
using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// Everything a revert is decided from for one pairing.
/// </summary>
/// <remarks>
/// A revert takes back what arrived through a sync, which was decided on #1 on
/// 2026-08-09 and is argued in <c>docs/lifecycle.md</c> beside the two answers
/// that were weighed and not taken. This is what that decision is carried out
/// against: the pairing whose rows are to be undone, and the items as this
/// server holds them now.
/// </remarks>
public sealed class RevertRequest
{
    /// <summary>
    /// Gets the pairing whose writes are to be taken back.
    /// </summary>
    public Guid PairingId { get; init; }

    /// <summary>
    /// Gets the items this revert considers, as they stand now.
    /// </summary>
    /// <remarks>
    /// An item this plugin wrote to that is not here is not reverted and is
    /// counted, because a value cannot be put back on an item nobody read. An
    /// item here that this plugin never wrote to costs a comparison and changes
    /// nothing, which is the correct direction for a caller that read a whole
    /// library rather than a selection.
    /// </remarks>
    public Collection<RevertObservation> Items { get; } = new();
}
