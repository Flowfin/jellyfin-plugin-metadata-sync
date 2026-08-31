using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.MetadataSync.Conflicts;

/// <summary>
/// Reads a pairing's decisions as the lines an operator opens, which is one
/// line per rule and outcome rather than one per field on one item.
/// </summary>
/// <remarks>
/// IT IS A READING OF THE ENTRIES AND HOLDS NOTHING, which is #48's second
/// condition rather than a property of the display it feeds. The groups are
/// computed from the rows whenever somebody asks and are gone afterwards, so
/// there is no second account of a pass to disagree with the first. A grouping
/// written down as the decisions were taken would be exactly that second
/// account, and the day it disagreed with the rows neither would be worth
/// reading - which is the argument <see cref="ConflictEntries"/> already makes
/// one level down, for the same reason.
/// <para>
/// So this takes the entries and never the store. It cannot be handed a
/// pairing, it opens no file and it asks nothing where the rows came from,
/// which is what makes the grouping of an exported account and the grouping of
/// a live one the same function over the same input rather than two routes that
/// agree today.
/// </para>
/// <para>
/// THE ORDER IS THE ACCOUNT'S OWN AND IS NOT SORTED. A group appears where its
/// first decision appeared, and the decisions inside it stay in the order the
/// account holds them, which is oldest first. Sorting by size would put the
/// largest group at the top, and the largest group in a healthy pass is the
/// rule that is working; the one an operator is looking for is the one that
/// started happening, and where it started is what says so.
/// </para>
/// <para>
/// What it does not do. It does not bound anything - what the account lost is
/// the store's bound and travels on <c>ConflictAccount</c> rather than in here,
/// because a group counting what it cannot see would be the clean number #66
/// refuses. It does not decide what an entry is owed for, which is
/// <see cref="ConflictEntries.IsOwed"/>. And it shows nothing: no surface in
/// this plugin reads any of this yet, which is the rest of #48.
/// </para>
/// </remarks>
public static class ConflictGrouping
{
    /// <summary>
    /// The groups a set of decisions falls into, in the order the decisions
    /// were held in.
    /// </summary>
    /// <param name="entries">The decisions, as the account holds them.</param>
    /// <returns>
    /// One group per rule and outcome that occurs, which is empty for an account
    /// with nothing to tell.
    /// </returns>
    /// <exception cref="ArgumentNullException">There are no decisions to read.</exception>
    /// <remarks>
    /// An account with no entries answers with no groups rather than with one
    /// group holding nothing. A pass that decided nothing and a pass whose
    /// decisions were all one thing are different facts about a library, and an
    /// empty group would be a line an operator opens to find nothing under it.
    /// </remarks>
    public static IReadOnlyList<ConflictGroup> Of(IReadOnlyList<ConflictEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        // The key is the pair rather than either half, and the rule is compared
        // by ordinal equality with null kept as null. A comparer folding null
        // into the empty string would file "the table ran out" under a rule
        // somebody had named with one, which is the one distinction this
        // grouping exists to keep.
        var order = new List<(string? Rule, ConflictOutcome Outcome)>();
        var members = new Dictionary<(string? Rule, ConflictOutcome Outcome), List<ConflictEntry>>();

        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);

            var key = (entry.Rule, entry.Outcome);

            if (!members.TryGetValue(key, out var held))
            {
                held = new List<ConflictEntry>();
                members[key] = held;
                order.Add(key);
            }

            held.Add(entry);
        }

        var groups = new List<ConflictGroup>(order.Count);

        foreach (var key in order)
        {
            groups.Add(new ConflictGroup
            {
                Rule = key.Rule,
                Outcome = key.Outcome,
                Entries = new ReadOnlyCollection<ConflictEntry>(members[key]),
            });
        }

        return new ReadOnlyCollection<ConflictGroup>(groups);
    }
}
