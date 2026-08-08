using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.MetadataSync.Fields;

/// <summary>
/// The one place a field is written from one server's item onto another's.
/// </summary>
/// <remarks>
/// Every write goes through <see cref="Move"/>, and <see cref="Move"/> asks the
/// register first, so a field with no row is refused at run time rather than
/// written quietly. The set of fields this type can write is asserted against
/// the register by the suite, in both directions: a writer with no row fails,
/// and a row that moves with no writer fails.
///
/// A lock the operator set on the receiving item refuses the write here, before
/// the writer runs. That is one half of the rule in #13 and the half this tree
/// can hold: the peer's lock state refuses a send rather than a write, and it
/// travels in an answer from a contract this plugin does not yet reference.
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
