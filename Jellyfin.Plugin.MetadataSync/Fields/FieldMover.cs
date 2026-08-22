using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.MetadataSync.Fields;

/// <summary>
/// Writes one declared field from one server's item onto another's, refusing
/// before the writer runs where the register or a lock says it may not.
/// </summary>
/// <remarks>
/// This is not the write path a pass takes, and the sentence here used to say
/// it was. A pass decides in <see cref="Reconciliation.Planner"/> and writes in
/// <see cref="Reconciliation.LibraryPlanTarget"/>, which obeys the flag the
/// decision left on the row without asking the register, the rules or a lock a
/// second time, because a second answer at write time is how the two halves of
/// one pass come to disagree about one field. Nothing outside the suite calls
/// <see cref="Move"/>.
///
/// So the refusals below are not what keeps a locked field out of a library.
/// The planner is, and <c>LockedFieldPlanTests</c> holds every lock the server
/// declares against it, each proved by taking the answer away from the planner
/// and watching the sweep redden. What these refusals hold is this call: a
/// field with no row, a row the register refuses to move, a locked item and a
/// locked field are each refused before the writer runs, so a caller reaching
/// for a writer directly cannot get past the register that way.
///
/// Whether a type that writes a field belongs here at all, now that a pass
/// writes through a plan, is #13's to settle rather than this file's. What
/// stops it drifting in the meantime is the suite holding the set of fields it
/// can write against the register in both directions: a writer with no row
/// fails, and a row that moves with no writer fails.
///
/// The peer's lock is not read on this call at all. It refuses a send rather
/// than a write, and it travels in an answer from a contract this plugin does
/// not yet reference.
///
/// What this type does not decide: which of the two values wins, whether the
/// item is the right item, and whether the field applies to the kind of item in
/// hand. Those are the planner, the resolver and the row's kind column, and none
/// of them is enforced here. It also does not record a refusal anywhere, because
/// there is nothing yet to record into; #48 is the conflict log a lock refusal
/// owes an entry in.
/// </remarks>
public static class FieldMover
{
    private static readonly IReadOnlyDictionary<string, Action<BaseItem, BaseItem>> _writers =
        new ReadOnlyDictionary<string, Action<BaseItem, BaseItem>>(
            new Dictionary<string, Action<BaseItem, BaseItem>>(StringComparer.Ordinal)
            {
                ["Name"] = static (from, to) => to.Name = from.Name,
                ["Overview"] = static (from, to) => to.Overview = from.Overview,
                ["Tagline"] = static (from, to) => to.Tagline = from.Tagline,
                ["Tags"] = static (from, to) => to.Tags = Copy(from.Tags),
                ["ProductionLocations"] = static (from, to) => to.ProductionLocations = Copy(from.ProductionLocations),
                ["OfficialRating"] = static (from, to) => to.OfficialRating = from.OfficialRating,
                ["PremiereDate"] = static (from, to) => to.PremiereDate = from.PremiereDate,
                ["ProductionYear"] = static (from, to) => to.ProductionYear = from.ProductionYear,
                ["EndDate"] = static (from, to) => to.EndDate = from.EndDate,
            });

    /// <summary>
    /// Gets the fields this type can write. The suite holds this set against the
    /// register, so it cannot drift from the rows that declare a field moves.
    /// </summary>
    public static IReadOnlyCollection<string> WritableFields => (IReadOnlyCollection<string>)_writers.Keys;

    /// <summary>
    /// Writes one declared field from one item onto another.
    /// </summary>
    /// <param name="field">The field, named as the server names it.</param>
    /// <param name="from">The item the value is taken from.</param>
    /// <param name="to">The item the value is written to.</param>
    /// <exception cref="FieldNotDeclaredException">
    /// The register declares no such field, declares one that does not move, or
    /// declares one this type has no writer for.
    /// </exception>
    /// <exception cref="FieldLockedException">
    /// The operator has locked the receiving item, or has locked the field the
    /// row is governed by on it.
    /// </exception>
    public static void Move(string field, BaseItem from, BaseItem to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var row = FieldRegister.RequireMovable(field);

        // The item-level lock is checked first because it is the wider claim and
        // the answer to it does not depend on the row. An operator who locked the
        // item said nothing on it is ours, and a field-level answer underneath
        // that would be reasoning about a question already settled.
        if (to.IsLocked)
        {
            throw new FieldLockedException(TheWholeItemIsLocked(field));
        }

        var locked = to.LockedFields;
        if (row.Lock is { } governing && locked is not null && Array.IndexOf(locked, governing) >= 0)
        {
            throw new FieldLockedException(TheFieldIsLocked(field, governing));
        }

        // Indexed rather than guarded. A row that moves with no writer behind it
        // is refused by the suite, which holds the two sets against each other in
        // both directions, so a guard here would be a line nothing can reach and
        // a guard nothing can reach proves nothing.
        _writers[field](from, to);
    }

    private static string TheWholeItemIsLocked(string field) => string.Format(
        CultureInfo.InvariantCulture,
        "The receiving item is locked, so '{0}' is not written and no other field on it is either. An operator locked the item because they keep its metadata themselves.",
        field);

    private static string TheFieldIsLocked(string field, MetadataField governing) => string.Format(
        CultureInfo.InvariantCulture,
        "The receiving item has '{0}' locked, which governs '{1}', so it is not written. An operator locked it because something kept overwriting it.",
        governing,
        field);

    private static string[] Copy(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        var copy = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            copy[i] = values[i];
        }

        return copy;
    }
}
