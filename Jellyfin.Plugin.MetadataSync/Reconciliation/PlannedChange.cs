using Jellyfin.Plugin.MetadataSync.Conflicts;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// What the plan says about one field on one item.
/// </summary>
/// <remarks>
/// This is the row a dry run shows, the row the conflict log records, the row
/// the administrator surface displays and the row the applier acts on. One
/// shape for all four, because four shapes of the same decision is four places
/// for it to be described differently.
/// <para>
/// <see cref="Writes"/> is stored rather than worked out from
/// <see cref="Outcome"/>. The applier reads this row and must not be able to
/// re-derive a decision from the rule set, so the decision arrives already
/// made and the applier has one boolean to obey. The suite holds the two
/// against each other, so a stored flag that disagrees with the outcome it came
/// from is refused rather than trusted.
/// </para>
/// </remarks>
public sealed class PlannedChange
{
    /// <summary>
    /// Gets the field, named as the server names it.
    /// </summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>
    /// Gets the value this server holds.
    /// </summary>
    public string? LocalValue { get; init; }

    /// <summary>
    /// Gets the value the peer holds.
    /// </summary>
    public string? PeerValue { get; init; }

    /// <summary>
    /// Gets how far the field got and which authority answered.
    /// </summary>
    public PlanDisposition Disposition { get; init; }

    /// <summary>
    /// Gets what the conflict rules decided, or null where the field never
    /// reached them.
    /// </summary>
    public ConflictOutcome? Outcome { get; init; }

    /// <summary>
    /// Gets the declared conflict rule that produced the outcome, by the name
    /// it is declared under.
    /// </summary>
    /// <remarks>
    /// Null in two different situations, and they are told apart by
    /// <see cref="Disposition"/> rather than by this column. A field that never
    /// reached the rules has no rule because it was answered earlier. A field
    /// that reached them and came back with none is the floor: the table ran
    /// out, nothing is written, and the difference belongs to an operator.
    /// </remarks>
    public string? Rule { get; init; }

    /// <summary>
    /// Gets a value indicating whether carrying this plan out writes this
    /// field.
    /// </summary>
    public bool Writes { get; init; }

    /// <summary>
    /// Gets the value that would be written, or null where nothing is written.
    /// </summary>
    /// <remarks>
    /// Always the peer's value where it is set at all, never a third value
    /// built out of the two. The resolver holds that invariant and this row
    /// carries what it handed back.
    /// </remarks>
    public string? ValueToWrite { get; init; }

    /// <summary>
    /// Gets the sentence this row is explained by, written for whoever is
    /// reading a plan and wondering why their field is in it.
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}
