using System;
using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// One item, as the two servers hold it, written down as plain values.
/// </summary>
/// <remarks>
/// The two identifiers are already resolved. Deciding that a local item and a
/// peer item are the same item is the pairing plugin's answer and never this
/// plugin's, which is #27, so a planner that received an item pair has nothing
/// left to decide about identity and cannot reach for a filename to help it.
/// <para>
/// The kind is the name the server gives the item type, and the register's kind
/// groups are lists of those names. It is a string rather than an enum because
/// the register is data and its groups are edited there: an enum here would be
/// a second declaration of the same set, in code, drifting against the rows.
/// </para>
/// </remarks>
public sealed class ItemObservation
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
    /// Gets a value indicating whether the operator has locked the whole item
    /// on this server.
    /// </summary>
    public bool ItemLockedHere { get; init; }

    /// <summary>
    /// Gets the token that says which version of this item on this server was
    /// read, or null where nobody read one.
    /// </summary>
    /// <remarks>
    /// It is a string and not a time, and that is the point rather than an
    /// accident. The only thing anything does with it is compare it for equality
    /// against the same item's token at the moment of writing, which is how a
    /// pass notices that something else wrote the item in between. Nothing orders
    /// two of them and nothing subtracts them, so there is no clock on the
    /// planner's input surface and the suite refuses one.
    /// <para>
    /// <see cref="LibraryPlanTarget.StampOf"/> is the one place the token is
    /// derived from an item, so whoever reads items into observations takes it
    /// from there rather than spelling it a second way.
    /// </para>
    /// </remarks>
    public string? LastSavedHere { get; init; }

    /// <summary>
    /// Gets the fields to consider on this item.
    /// </summary>
    /// <remarks>
    /// Whichever fields the caller read. The planner answers for every one of
    /// them, including the ones it refuses, because a field that produced no
    /// row in the plan is indistinguishable from a field nobody looked at.
    /// </remarks>
    public Collection<FieldObservation> Fields { get; } = new();
}
