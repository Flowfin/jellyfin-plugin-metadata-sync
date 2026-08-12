using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Fields;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// The half of a pass that decides, and the only half that decides anything.
/// </summary>
/// <remarks>
/// It takes what two servers hold and returns what should change. It reads no
/// library, opens no file, makes no request and asks no clock: the whole input
/// is a <see cref="PlanRequest"/> somebody wrote down, so every rule in this
/// plan is arranged in a test as a table and run with nothing else in the room.
/// There is nothing here to substitute, which is stronger than everything here
/// being substitutable.
/// <para>
/// It decides nothing of its own either. Whether a field may move is the
/// register's answer, whether an operator narrowed that further is the
/// configuration's, and which of two values survives is the conflict rules'.
/// This type is the order those three are asked in, and the order is the only
/// thing it contributes: the register first, because a field that may not move
/// is not a conflict; the operator's exclusions second, because they subtract
/// from what the register allows; the rules last, on what is left.
/// </para>
/// <para>
/// What it does not do. It does not decide that two items are the same item,
/// which arrives already answered in an <see cref="ItemObservation"/> and is
/// the pairing plugin's to answer. It does not record anything, which is #48.
/// It does not write, which is <see cref="Applier"/>. And it does not know
/// which items to consider or which libraries take part, which is decided
/// before a request reaches it.
/// </para>
/// </remarks>
public static class Planner
{
    /// <summary>
    /// Decides what a pass would change.
    /// </summary>
    /// <param name="request">What the two servers hold, and what the operator asked for.</param>
    /// <returns>The plan, which is data and changes nothing by existing.</returns>
    /// <exception cref="ArgumentNullException">There is no request to plan from.</exception>
    public static Plan Plan(PlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var excluded = new HashSet<string>(request.ExcludedFields, StringComparer.Ordinal);

        var plan = new Plan
        {
            PairingId = request.PairingId,
            Direction = request.Direction,
        };

        foreach (var item in request.Items)
        {
            var itemPlan = new ItemPlan
            {
                LocalItemId = item.LocalItemId,
                PeerItemId = item.PeerItemId,
                Kind = item.Kind,
            };

            foreach (var observation in item.Fields)
            {
                itemPlan.Changes.Add(Decide(item, observation, excluded));
            }

            plan.Items.Add(itemPlan);
        }

        return plan;
    }

    /// <summary>
    /// Decides one field, by asking the three authorities in order and stopping
    /// at the first one that answers.
    /// </summary>
    private static PlannedChange Decide(
        ItemObservation item,
        FieldObservation observation,
        HashSet<string> excluded)
    {
        var row = FieldRegister.Find(observation.Field);

        if (row is null)
        {
            return Answered(observation, PlanDisposition.NotDeclared, NoRowAtAll(observation.Field));
        }

        if (!Contains(row.Kinds, item.Kind))
        {
            return Answered(observation, PlanDisposition.OutsideTheKindGroup, NotThisKind(row, item.Kind));
        }

        if (!row.Moves)
        {
            return Answered(observation, PlanDisposition.DoesNotMove, row.OperatorReason);
        }

        if (excluded.Contains(observation.Field))
        {
            return Answered(observation, PlanDisposition.ExcludedByTheOperator, Excluded(observation.Field));
        }

        // The register says which lock governs the field, and the observation
        // says what each side reported. A field the register governs by no lock
        // is not locked by a side claiming it is: the register is the authority
        // for which claim means anything, and the item-level lock is the only
        // instrument left for such a field. That is resolved here rather than
        // in the rules, because a rule that had to ask the register would be a
        // rule that could not be run from a table.
        var decision = new ConflictResolver().Resolve(new ConflictInputs
        {
            LocalValue = observation.LocalValue,
            PeerValue = observation.PeerValue,
            LastWrittenByThisPlugin = observation.LastWrittenByThisPlugin,
            ItemLockedHere = item.ItemLockedHere,
            FieldLockedHere = row.Lock is not null && observation.FieldLockedHere,
            FieldLockedOnPeer = row.Lock is not null && observation.FieldLockedOnPeer,
        });

        var writes = decision.Outcome == ConflictOutcome.TakePeer;

        return new PlannedChange
        {
            Field = observation.Field,
            LocalValue = observation.LocalValue,
            PeerValue = observation.PeerValue,
            Disposition = PlanDisposition.Decided,
            Outcome = decision.Outcome,
            Rule = decision.Rule?.Id,
            Writes = writes,
            ValueToWrite = writes ? decision.Value : null,
            Reason = decision.Rule?.Reason ?? NoRuleFired(observation.Field),
        };
    }

    /// <summary>
    /// Builds a row for a field that never reached the conflict rules. Nothing
    /// is written and no rule is named, because none was asked.
    /// </summary>
    private static PlannedChange Answered(FieldObservation observation, PlanDisposition disposition, string reason)
    {
        return new PlannedChange
        {
            Field = observation.Field,
            LocalValue = observation.LocalValue,
            PeerValue = observation.PeerValue,
            Disposition = disposition,
            Outcome = null,
            Rule = null,
            Writes = false,
            ValueToWrite = null,
            Reason = reason,
        };
    }

    private static bool Contains(IReadOnlyList<string> kinds, string kind)
    {
        foreach (var candidate in kinds)
        {
            if (string.Equals(candidate, kind, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NoRowAtAll(string field) => string.Format(
        CultureInfo.InvariantCulture,
        "The field register declares no field named '{0}'. Nothing knows what a wrong value there would cost, so it is not moved.",
        field);

    private static string NotThisKind(FieldRow row, string kind) => string.Format(
        CultureInfo.InvariantCulture,
        "The register declares '{0}' for the '{1}' kinds, and this item is a '{2}', so the row does not apply to it.",
        row.Field,
        row.KindGroup,
        kind);

    private static string Excluded(string field) => string.Format(
        CultureInfo.InvariantCulture,
        "The register allows '{0}' to move and this plugin's configuration excludes it, so it is not moved.",
        field);

    private static string NoRuleFired(string field) => string.Format(
        CultureInfo.InvariantCulture,
        "The two servers differ on '{0}' and no declared rule answers it. Neither value is written and the difference is for an operator to settle.",
        field);
}
