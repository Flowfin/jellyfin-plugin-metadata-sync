using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    /// Gets the token that says which version of the item on this server the
    /// plan was made from.
    /// </summary>
    /// <remarks>
    /// Carried so the half that writes can tell whether anything else wrote the
    /// item since. A plan that carries none cannot answer that question, and the
    /// write path refuses one rather than writing without the answer. It is
    /// compared for equality and never ordered, which is why it is a string here
    /// and not a time.
    /// </remarks>
    public string? LastSavedWhenPlanned { get; init; }

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
    public bool Writes => Changes.Any(change => change.Writes);

    /// <summary>
    /// Gets how many fields on this item would be written.
    /// </summary>
    [JsonIgnore]
    public int FieldsToWrite => Changes.Count(change => change.Writes);
}
