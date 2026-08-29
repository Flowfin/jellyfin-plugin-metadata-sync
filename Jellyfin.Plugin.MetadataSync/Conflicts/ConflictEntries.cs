using System;
using System.Collections.Generic;
using Jellyfin.Plugin.MetadataSync.Reconciliation;

namespace Jellyfin.Plugin.MetadataSync.Conflicts;

/// <summary>
/// Turns a plan into the entries an operator is owed an account of.
/// </summary>
/// <remarks>
/// A plan is the same object four times over and this is one of the four
/// readings of it, which is why the entries are derived from a plan rather than
/// written beside one as it is made. A second route that recorded decisions as
/// they were taken would be a second account of the same pass, and the day the
/// two disagreed neither would be worth reading.
/// </remarks>
public static class ConflictEntries
{
    /// <summary>
    /// Every entry a plan owes, in the order the plan considered the fields.
    /// </summary>
    /// <param name="plan">The plan the pass decided.</param>
    /// <param name="at">The moment to record on every entry, on this server's clock.</param>
    /// <returns>The entries, which is empty for a pass that found nothing to tell.</returns>
    /// <remarks>
    /// The moment is handed in rather than read here, for the reason written on
    /// <see cref="ConflictEntry.At"/>. One plan produces one moment, so the
    /// entries of a pass sort together instead of being spread across however
    /// long the pass took.
    /// </remarks>
    public static IReadOnlyList<ConflictEntry> From(Plan plan, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var entries = new List<ConflictEntry>();

        foreach (var item in plan.Items)
        {
            foreach (var change in item.Changes)
            {
                if (!IsOwed(change))
                {
                    continue;
                }

                entries.Add(new ConflictEntry
                {
                    Item = item.LocalItemId,
                    Field = change.Field,
                    LocalValue = ShownValue.Of(change.LocalValue),
                    PeerValue = ShownValue.Of(change.PeerValue),
                    Rule = change.Rule,
                    Outcome = change.Outcome!.Value,
                    Direction = plan.Direction,
                    At = at,
                });
            }
        }

        return entries;
    }

    /// <summary>
    /// Whether one row of a plan is a decision somebody would ask about.
    /// </summary>
    /// <param name="change">The row.</param>
    /// <returns>True where the row owes an entry.</returns>
    /// <remarks>
    /// Two conditions, and both are stated here rather than left to whoever
    /// writes the register.
    /// <para>
    /// A row that never reached the conflict rules is not a conflict. A field
    /// the register does not declare, one it declares as never moving, one
    /// outside this kind of item and one the operator excluded were all settled
    /// before the two values were compared at all, and an account of them is an
    /// account of the register rather than of a disagreement.
    /// </para>
    /// <para>
    /// A row where the two servers already say the same thing is a decision
    /// with nothing to tell. Everything else earns an entry, the first pass
    /// filling an empty field included: a value that appeared in a library
    /// overnight is the first thing an operator asks about, and an account that
    /// left out the writes would answer every question except that one.
    /// </para>
    /// <para>
    /// What that comparison costs, stated rather than discovered. It is the
    /// texts as the rules were handed them, so two spellings of nothing - no
    /// value on one side and a single space on the other - are two different
    /// texts here and earn an entry the rules answered as agreement. That is one
    /// entry too many rather than a decision left out, which is the direction
    /// this account fails in.
    /// </para>
    /// </remarks>
    public static bool IsOwed(PlannedChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return change.Disposition == PlanDisposition.Decided
            && change.Outcome is not null
            && !string.Equals(change.LocalValue, change.PeerValue, StringComparison.Ordinal);
    }
}
