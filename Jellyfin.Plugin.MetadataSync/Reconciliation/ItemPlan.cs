using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// What the plan says about one item.
/// </summary>
// The rows are filled in place for the same reason the plan's items are, and
// the reason is written there.
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class ItemPlan
{
    /// <summary>
    /// Gets the item on this server.
    /// </summary>
    public Guid LocalItemId { get; init; }

    /// <summary>
    /// Gets the item on the peer that this one was resolved to.
    /// </summary>
    public Guid PeerItemId { get; init; }

    /// <summary>
    /// Gets the kind of item, named as the server names the type.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets what the plan says about each field considered on this item, in the
    /// order they were considered.
    /// </summary>
    public Collection<PlannedChange> Changes { get; } = new();

    /// <summary>
    /// Gets a value indicating whether carrying this plan out writes anything
    /// to this item.
    /// </summary>
    /// <remarks>
    /// Derived from the rows rather than stored beside them, so there is no
    /// second copy of the answer to disagree with the first. This is the one
    /// thing the applier asks about an item.
    /// </remarks>
    [JsonIgnore]
    public bool Writes
    {
        get
        {
            foreach (var change in Changes)
            {
                if (change.Writes)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets how many fields on this item would be written.
    /// </summary>
    [JsonIgnore]
    public int FieldsToWrite
    {
        get
        {
            var count = 0;
            foreach (var change in Changes)
            {
                if (change.Writes)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
