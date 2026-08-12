namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// One field on one item, as the two servers hold it, written down as plain
/// values before anything decides what to do about it.
/// </summary>
/// <remarks>
/// This is the planner's input surface for a field and it is deliberately
/// narrow. There is no item here, no library type, no contract type and no
/// clock: whoever reads the two servers turns what they found into these
/// values, and everything downstream decides from them alone. That is what
/// lets the whole decision surface be arranged by writing a table down.
/// <para>
/// The two lock columns are what each side reports, not what governs the
/// field. Which lock governs a field is the register's answer, and the planner
/// resolves the two together, so a claim that a field is locked here for a
/// field the register governs by no lock decides nothing.
/// </para>
/// </remarks>
public readonly record struct FieldObservation
{
    /// <summary>
    /// Gets the field, named as the server names it on the item type.
    /// </summary>
    public required string Field { get; init; }

    /// <summary>
    /// Gets the value this server holds, or null where it holds none.
    /// </summary>
    public required string? LocalValue { get; init; }

    /// <summary>
    /// Gets the value the peer holds, or null where it holds none.
    /// </summary>
    public required string? PeerValue { get; init; }

    /// <summary>
    /// Gets the value this plugin last wrote for this field on this item, or
    /// null where it has never written one.
    /// </summary>
    /// <remarks>
    /// The store this comes from is #47 and it is not built, so today every
    /// caller hands in null and every case that turns on it is arranged in a
    /// test. That is worth knowing when reading a plan: a null here says this
    /// plugin has no record of writing the field, and until the store exists it
    /// says that about every field.
    /// </remarks>
    public required string? LastWrittenByThisPlugin { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server lock that could govern this
    /// field is set on the item on this server.
    /// </summary>
    public required bool FieldLockedHere { get; init; }

    /// <summary>
    /// Gets a value indicating whether the peer reports the lock that could
    /// govern this field as set on their side.
    /// </summary>
    /// <remarks>
    /// Nothing in this tree can populate this yet. A peer's lock state travels
    /// in an answer from the pairing contract, which this plugin does not
    /// reference, so the column exists for the rule to be written against and
    /// is false everywhere outside a test until the contract carries it.
    /// </remarks>
    public required bool FieldLockedOnPeer { get; init; }
}
