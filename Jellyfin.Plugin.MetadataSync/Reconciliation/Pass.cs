using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Store;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// One pass, from what the two servers hold to what was written, arranged so
/// that a pass which was stopped can be continued rather than started again.
/// </summary>
/// <remarks>
/// It adds one thing to the two halves it drives, and that thing is an ordering.
/// <see cref="Planner"/> decides, <see cref="Applier"/> writes, and neither of
/// them knows that a pass can be interrupted. This type records an item as
/// finished with after the applier has returned from it, and reads those records
/// back at the start of the next pass over the same pairing, so the work an
/// interrupted pass did is not done twice.
/// <para>
/// IT RESUMES AND IT NEVER REPLAYS, and the two are worth separating because a
/// stored plan is the obvious way to build the second one by accident. What
/// survives an interruption here is a set of item identifiers and nothing that
/// could be obeyed: the items are observed again by the caller, the plan for
/// what is left is built again by the planner, and the values written are the
/// ones the two servers hold when the resumed pass runs. A plan stored at the
/// interruption and replayed afterwards would write the value the peer held
/// before the interruption, over a value the peer has since changed, which is
/// the failure this shape exists against. Nothing in this plugin serialises a
/// plan, and the suite refuses a store made of one rather than trusting this
/// paragraph.
/// </para>
/// <para>
/// The items are applied one at a time rather than as one plan, and that is the
/// whole reason this type drives the applier instead of calling it once. The
/// moment between an item's write and the record that the item is done has to be
/// somewhere a resume can reason about; inside a loop over a whole plan it is
/// nowhere at all.
/// </para>
/// <para>
/// A deferred item is not recorded as finished with. An item the write path
/// handed back as deferred was not written, so the pass that runs next reaches
/// it again, which is the difference between an item this pass decided about and
/// one it was kept away from. An item that was considered and had nothing to
/// write IS recorded, because it was decided about, and the cost of that is
/// stated in <c>docs/storage.md</c>: a first pass over a library that changes
/// little still records a line per item.
/// </para>
/// <para>
/// What it does not do. It does not read either server, which happens before a
/// request exists. It does not decide anything, which is the planner's. It does
/// not report how far a stopped pass got, which is #37: a stopped pass throws
/// where it was stopped, and what it got through is on the disk rather than in a
/// value nobody received.
/// </para>
/// </remarks>
public sealed class Pass
{
    private readonly Applier _applier;
    private readonly IPassProgress _progress;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pass"/> class.
    /// </summary>
    /// <param name="applier">The half of a pass that writes.</param>
    /// <param name="progress">The record of how far a pass got.</param>
    /// <exception cref="ArgumentNullException">There is nothing to write through, or nowhere to record how far the pass got.</exception>
    /// <remarks>
    /// The progress record is required rather than optional, for the reason the
    /// applier's store is. A pass that could be built without one would have a
    /// path on which an interruption loses everything the pass did, and the next
    /// pass would write the whole library again; a default argument would be the
    /// way that arrives.
    /// </remarks>
    public Pass(Applier applier, IPassProgress progress)
    {
        ArgumentNullException.ThrowIfNull(applier);
        ArgumentNullException.ThrowIfNull(progress);

        _applier = applier;
        _progress = progress;
    }

    /// <summary>
    /// Runs one pass over what the caller observed, continuing an interrupted
    /// pass over the same pairing where there is one.
    /// </summary>
    /// <param name="request">What the two servers hold, and what the operator asked for.</param>
    /// <param name="cancellationToken">Stops a pass an operator asked to stop.</param>
    /// <returns>What the pass did, where it ran to the end.</returns>
    /// <exception cref="ArgumentNullException">There is no request to run from.</exception>
    /// <remarks>
    /// The refusal is in this method and the work is in the one below it, so a
    /// caller with no request is refused as it calls rather than whenever it
    /// gets round to awaiting.
    /// </remarks>
    public Task<PassResult> RunAsync(PlanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Run(request, cancellationToken);
    }

    private async Task<PassResult> Run(PlanRequest request, CancellationToken cancellationToken)
    {
        // Read once, at the start, rather than per item. What an earlier pass
        // finished with cannot grow while this pass runs, since this pass is the
        // only thing writing it, and a read per item would make the answer
        // depend on rows this pass had just added.
        var alreadyDone = new HashSet<Guid>(_progress.CompletedItems(request.PairingId));

        var remaining = new PlanRequest
        {
            PairingId = request.PairingId,
            Direction = request.Direction,
        };

        foreach (var field in request.ExcludedFields)
        {
            remaining.ExcludedFields.Add(field);
        }

        var skipped = 0;

        foreach (var item in request.Items)
        {
            if (alreadyDone.Contains(item.LocalItemId))
            {
                skipped++;
                continue;
            }

            remaining.Items.Add(item);
        }

        // Planned here rather than by the caller, and planned from what is left
        // rather than from everything. A plan built over the whole library and
        // then filtered would have asked the conflict rules about items this
        // pass is not going to touch, and the plan an operator is shown would
        // name changes this pass will not make.
        var plan = Planner.Plan(remaining);

        var itemsWritten = 0;
        var fieldsWritten = 0;
        var itemsPassedOver = 0;
        var itemsDeferred = 0;

        foreach (var item in plan.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var one = new Plan
            {
                PairingId = plan.PairingId,
                Direction = plan.Direction,
            };

            one.Items.Add(item);

            var applied = await _applier.ApplyAsync(one, cancellationToken).ConfigureAwait(false);

            itemsWritten += applied.ItemsWritten;
            fieldsWritten += applied.FieldsWritten;
            itemsPassedOver += applied.ItemsPassedOver;
            itemsDeferred += applied.ItemsDeferred;

            // After the applier returned and never before it. A record written
            // first claims an item is done that the write may refuse, and the
            // resume then skips an item nothing wrote, which is a library left
            // unsynced with nothing saying so. Written last, an interruption in
            // between costs the item being written a second time, and writing an
            // item a second time writes the values it already holds.
            //
            // A deferred item is not recorded, because it was not written. The
            // pass that runs next reaches it again.
            if (applied.ItemsDeferred == 0)
            {
                _progress.Completed(request.PairingId, item.LocalItemId);
            }
        }

        // The pass finished, so what it recorded is no longer anybody's resume
        // point. Cleared here rather than at the next pass's start: a record
        // left behind would make the next pass over this pairing skip every item
        // this one wrote.
        _progress.Cleared(request.PairingId);

        return new PassResult
        {
            ItemsWritten = itemsWritten,
            FieldsWritten = fieldsWritten,
            ItemsPassedOver = itemsPassedOver,
            ItemsDeferred = itemsDeferred,
            ItemsAlreadyDone = skipped,
        };
    }
}
