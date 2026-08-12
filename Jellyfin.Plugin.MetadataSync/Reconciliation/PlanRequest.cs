using System;
using System.Collections.ObjectModel;
using Jellyfin.Plugin.MetadataSync.Configuration;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// Everything the planner is given for one pass.
/// </summary>
/// <remarks>
/// Reading the two servers, deciding which libraries take part and turning
/// items into observations all happen before this type exists. What arrives
/// here is data, which is the property the planner is built for: a pass can be
/// arranged in a test by writing one of these down, with nothing running.
/// </remarks>
public sealed class PlanRequest
{
    /// <summary>
    /// Gets the pairing this pass is for.
    /// </summary>
    public Guid PairingId { get; init; }

    /// <summary>
    /// Gets which way metadata moves for this pairing.
    /// </summary>
    /// <remarks>
    /// It is carried through onto the plan and nothing here branches on it. A
    /// plan entry saying a field was not written is unreadable without knowing
    /// which way that field was ever going to move, which is why the value
    /// travels with the decisions rather than being looked up again by whoever
    /// reads them.
    /// </remarks>
    public SyncDirection Direction { get; init; }

    /// <summary>
    /// Gets the fields this operator has excluded, out of the fields the
    /// register allows to move.
    /// </summary>
    public Collection<string> ExcludedFields { get; } = new();

    /// <summary>
    /// Gets the items this pass considers.
    /// </summary>
    public Collection<ItemObservation> Items { get; } = new();
}
