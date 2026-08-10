namespace Jellyfin.Plugin.MetadataSync.Conflicts;

/// <summary>
/// Everything a conflict rule is allowed to know about one field on one item.
/// </summary>
/// <remarks>
/// This is the whole input surface of the resolver, and what is missing from it
/// is the point. There is no item, no library, no contract and no clock here:
/// a rule that cannot be decided from these six values is a rule this plan does
/// not have, and it cannot be added by reaching for something else at the site
/// that needs it.
/// <para>
/// The lock columns are answers rather than sources. Whether the register names
/// a server lock for this field, and whether that lock is set on the item here,
/// is resolved by the caller into <see cref="FieldLockedHere"/>, and what the
/// peer reports about its own side arrives as <see cref="FieldLockedOnPeer"/>.
/// That keeps the register lookup and the contract answer out of a type whose
/// whole value is that it is plain data.
/// </para>
/// <para>
/// A value is a string or it is absent, and absence is spelled two ways the
/// rules read as one: null, and text that is only whitespace. That is declared
/// in <c>docs/conflicts.md</c> rather than observed about the server, because an
/// overview of three spaces is what a provider writes when it found nothing.
/// </para>
/// </remarks>
public readonly record struct ConflictInputs
{
    /// <summary>
    /// Gets the value this server holds for the field, or null where it holds
    /// none.
    /// </summary>
    public required string? LocalValue { get; init; }

    /// <summary>
    /// Gets the value the peer holds for the field, or null where it holds
    /// none.
    /// </summary>
    public required string? PeerValue { get; init; }

    /// <summary>
    /// Gets the value this plugin last wrote for this field on this item, or
    /// null where it has never written one.
    /// </summary>
    /// <remarks>
    /// This is what separates an update from a conflict, and it is the reason
    /// this plan needs no clock. A local value equal to it is a value nobody
    /// here has expressed an opinion on since it arrived; a local value that
    /// differs from it was changed by somebody on this server, however old it
    /// looks. The record it comes from is #16.
    /// </remarks>
    public required string? LastWrittenByThisPlugin { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operator has locked the whole item
    /// on this server.
    /// </summary>
    public required bool ItemLockedHere { get; init; }

    /// <summary>
    /// Gets a value indicating whether the server lock the register names for
    /// this field is set on the item on this server.
    /// </summary>
    public required bool FieldLockedHere { get; init; }

    /// <summary>
    /// Gets a value indicating whether the peer reports the lock that governs
    /// this field as set on their side.
    /// </summary>
    public required bool FieldLockedOnPeer { get; init; }
}
