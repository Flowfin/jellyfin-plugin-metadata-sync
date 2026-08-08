using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MediaBrowser.Controller.Entities;

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
/// What this type does not decide: which of the two values wins, whether the
/// item is the right item, and whether the field applies to the kind of item in
/// hand. Those are the planner, the resolver and the row's kind column, and none
/// of them is enforced here.
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
    public static void Move(string field, BaseItem from, BaseItem to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        FieldRegister.RequireMovable(field);

        // Indexed rather than guarded. A row that moves with no writer behind it
        // is refused by the suite, which holds the two sets against each other in
        // both directions, so a guard here would be a line nothing can reach and
        // a guard nothing can reach proves nothing.
        _writers[field](from, to);
    }

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
