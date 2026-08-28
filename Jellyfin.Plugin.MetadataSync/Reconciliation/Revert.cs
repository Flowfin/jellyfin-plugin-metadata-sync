using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Store;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// Takes back what arrived through a sync, for a pairing that is over.
/// </summary>
/// <remarks>
/// The decision is #1's, taken on 2026-08-09 and argued in
/// <c>docs/lifecycle.md</c> beside the two answers that were weighed and not
/// taken. This is the mechanism, and it is a decision rather than an act: it
/// reads the record and what the library holds now, and answers with a
/// <see cref="RevertPlan"/> that changes nothing by existing. Applying it is the
/// ordinary write path's, so an item's fields go together or not at all and an
/// item something else has written since is deferred, neither of which is worth
/// a second implementation.
/// <para>
/// THE RULE IS #66's AND IT IS WHAT MAKES A REVERT ALLOWABLE AT ALL. A value is
/// put back only where this plugin can prove two things about it: that it wrote
/// what is there now, and that it can produce the value that was there before it
/// ever wrote. Anything else is left alone and counted. There is no branch that
/// reverts on an assumption, which is why the counts are part of the answer
/// rather than a report about it.
/// </para>
/// <para>
/// THE SECOND PROOF IS THE ONE TO READ CAREFULLY, because it is the one a
/// reader assumes is free. A field's history is bounded and the discard is not
/// recorded, which <c>docs/storage.md</c> states: a field this plugin wrote
/// eleven times and one it wrote exactly ten are the same store afterwards. So a
/// history standing AT the bound is one the bound may already have taken the
/// first write out of, and the earliest value still held may itself have come
/// from the peer. Restoring it would put the peer's own value back in the name
/// of removing it. A history SHORTER than the bound has had nothing discarded,
/// because values are dropped only once the bound is exceeded, and that is what
/// makes this decidable instead of assumed.
/// </para>
/// <para>
/// WHAT THAT BUYS IS THE IDEMPOTENCE #64's FIFTH CONDITION ASKS FOR, and it
/// holds in both directions. A field restored once holds the value this plugin
/// last wrote, so a revert run again over the same library either restores the
/// same value or, where the write that restored it took the history to the
/// bound, leaves it alone. The library does not move either way, which is what
/// an interrupted revert being run again has to mean.
/// </para>
/// <para>
/// It deletes nothing. Removing a field's value and removing the item that holds
/// it are different acts and only the first is ever in scope here; the walk in
/// the suite is what keeps that a property of this path rather than a sentence
/// about it. And it removes no record: what this plugin holds for a pairing goes
/// when an operator asks for it to go, which is #61's act and is not this one.
/// </para>
/// </remarks>
public static class Revert
{
    /// <summary>
    /// Decides what a revert would do.
    /// </summary>
    /// <param name="request">The pairing, and the items as this server holds them now.</param>
    /// <param name="written">The record of what this plugin wrote.</param>
    /// <returns>The plan and the counts, which change nothing by existing.</returns>
    /// <exception cref="ArgumentNullException">There is no request to decide from, or no record to decide against.</exception>
    public static RevertPlan Plan(RevertRequest request, IWrittenValues written)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(written);

        var plan = new Plan { PairingId = request.PairingId };

        var read = request.Items.Select(item => item.LocalItemId).ToHashSet();
        var noRecord = 0;
        var changedSince = 0;
        var notKnown = 0;

        foreach (var observation in request.Items)
        {
            var itemPlan = new ItemPlan
            {
                LocalItemId = observation.LocalItemId,
                PeerItemId = Guid.Empty,
                Kind = observation.Kind,
                LastSavedWhenPlanned = observation.LastSavedHere,
            };

            foreach (var field in observation.Fields)
            {
                var history = written.History(request.PairingId, observation.LocalItemId, field.Field);

                if (history.Count == 0)
                {
                    // No record at all. The field is not this plugin's, and a
                    // missing record is evidence of that rather than an
                    // invitation to assume, which is #66's rule.
                    noRecord++;
                    continue;
                }

                if (!string.Equals(history[^1].Value, field.LocalValue, StringComparison.Ordinal))
                {
                    // The record exists and says this plugin wrote something
                    // else, so somebody here has changed the field since.
                    // Putting a value back over that would delete their edit.
                    changedSince++;
                    continue;
                }

                if (history.Count >= WrittenValues.Bound)
                {
                    // The history is at the bound, so the bound may already have
                    // dropped the write that came first, and the earliest value
                    // still held may itself have come from the peer. The discard
                    // is not recorded, so this cannot be asked - only bounded.
                    notKnown++;
                    continue;
                }

                itemPlan.Changes.Add(new PlannedChange
                {
                    Field = field.Field,
                    LocalValue = field.LocalValue,
                    PeerValue = null,
                    Disposition = PlanDisposition.Decided,
                    Rule = "revert-what-this-plugin-wrote",
                    Writes = true,
                    ValueToWrite = history[0].Previous,
                    Reason = Restoring(field.Field),
                });
            }

            if (itemPlan.Writes)
            {
                plan.Items.Add(itemPlan);
            }
        }

        return new RevertPlan
        {
            Plan = plan,
            FieldsWithNoRecord = noRecord,
            FieldsChangedSinceThisPluginWroteThem = changedSince,
            FieldsWhoseEarlierValueIsNotKnown = notKnown,
            FieldsOnItemsNotRead = written.Fields(request.PairingId).Count(key => !read.Contains(key.Item)),
        };
    }

    /// <summary>
    /// Why one field is being written, in the words a plan row carries.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The sentence.</returns>
    private static string Restoring(string field) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} holds what this plugin last wrote and its record reaches back to what was there before this plugin first wrote it, so that earlier value is put back.",
        field);
}
