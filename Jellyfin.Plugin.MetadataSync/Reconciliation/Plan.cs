using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.MetadataSync.Configuration;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// What a pass would do, decided and written down before anything is done.
/// </summary>
/// <remarks>
/// A plan is data and never a list of actions. Nothing on it is a delegate,
/// nothing on it holds an item, and nothing on it can be carried out by being
/// read: the applier is a separate half that takes one of these and calls the
/// library, and everything else that consumes a plan only looks at it.
/// <para>
/// That is what makes it the same object four times over. A dry run shows a
/// plan, the conflict log records rows out of one, the administrator surface
/// displays one, and the applier obeys one. Designed once, so the four cannot
/// describe the same decision differently.
/// </para>
/// </remarks>
// The rows are a read-only property the serialiser fills in place, and it does
// that only when it is told to. Its default is to replace a property, a
// property with no setter cannot be replaced, and without this attribute every
// item is dropped on the way back in with no error anywhere. That is the quiet
// direction of the failure: a plan read back with no items writes nothing and
// reports having applied itself.
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class Plan
{
    /// <summary>
    /// Gets the pairing this plan was made for.
    /// </summary>
    public Guid PairingId { get; init; }

    /// <summary>
    /// Gets the direction in force when the plan was made.
    /// </summary>
    public SyncDirection Direction { get; init; }

    /// <summary>
    /// Gets what the plan says about each item, in the order they were
    /// considered.
    /// </summary>
    public Collection<ItemPlan> Items { get; } = new();

    /// <summary>
    /// Gets how many fields the plan considered, across every item.
    /// </summary>
    [JsonIgnore]
    public int FieldsConsidered
    {
        get
        {
            var count = 0;
            foreach (var item in Items)
            {
                count += item.Changes.Count;
            }

            return count;
        }
    }

    /// <summary>
    /// Gets how many fields carrying the plan out would write.
    /// </summary>
    [JsonIgnore]
    public int FieldsToWrite
    {
        get
        {
            var count = 0;
            foreach (var item in Items)
            {
                count += item.FieldsToWrite;
            }

            return count;
        }
    }
}
