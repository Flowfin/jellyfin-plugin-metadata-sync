using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Configuration;
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
/// IT DOES NOT DERIVE THE PLAN ITSELF. <see cref="DryRun"/> holds that half,
/// and this type asks it for the plan rather than repeating the derivation, so
/// what an operator read before asking for an apply and what the apply carries
/// out are the same object. A first release plans and does not write, which
/// makes the plan-only route the one that ships and this one the route that is
/// asked for afterwards; a second derivation beside it would be the copy that
/// goes on describing a pass this type has stopped making.
/// </para>
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
/// A PASS AN OPERATOR STOPPED AND A PASS THAT RAN OUT OF TIME LEAVE BY
/// DIFFERENT DOORS, and the difference is the whole of #315. A cancellation
/// throws where it was stopped, because the caller asked for that and there is
/// nobody left to hand a result to. The time bound returns instead: the pass
/// stops at an item boundary, keeps what it recorded, and answers with a result
/// that says it did not finish. Both keep the resume point, and only a pass that
/// reached the end of the plan clears it.
/// </para>
/// <para>
/// THE CLOCK IS INJECTED AND IT IS ONE SERVER'S, WHICH IS THE THING TO READ
/// BEFORE ADDING A SECOND USE OF IT. What is measured here is elapsed time on
/// the machine this pass runs on, compared against a number out of the
/// configuration and against nothing on the peer. The invariant #46 declares is
/// about a stamp from one server held against the other's, and this cannot make
/// that comparison: no peer stamp is on any type this file can reach. The lint
/// row for that invariant carries an allowance naming this file, so a clock
/// arriving here is a decision a reader meets rather than an absence.
/// </para>
/// <para>
/// What it does not do. It does not read either server, which happens before a
/// request exists. It does not decide anything, which is the planner's. It does
/// not bound how many writes it makes per unit of time, which wants a
/// measurement against a real library and is the half of #37 still open.
/// </para>
/// </remarks>
public sealed class Pass
{
    private readonly Applier _applier;
    private readonly IPassProgress _progress;
    private readonly TimeProvider _time;
    private readonly TimeSpan _limit;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pass"/> class.
    /// </summary>
    /// <param name="applier">The half of a pass that writes.</param>
    /// <param name="progress">The record of how far a pass got.</param>
    /// <param name="time">The clock the pass's own elapsed time is read from.</param>
    /// <param name="limit">
    /// How long this pass may run, which is
    /// <see cref="PluginConfiguration.MinutesPerPass"/> for a pass an operator
    /// configured.
    /// </param>
    /// <exception cref="ArgumentNullException">There is nothing to write through, nowhere to record how far the pass got, or no clock to measure the pass against.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The pass is allowed no time at all, which is a pass that stops before its first item rather than a pass that runs briefly.</exception>
    /// <remarks>
    /// The progress record is required rather than optional, for the reason the
    /// applier's store is. A pass that could be built without one would have a
    /// path on which an interruption loses everything the pass did, and the next
    /// pass would write the whole library again; a default argument would be the
    /// way that arrives.
    /// <para>
    /// The bound is the same shape and is here for a sharper version of the same
    /// reason. There is deliberately no second constructor and no overload
    /// without it: an unbounded overload beside a bounded one leaves every
    /// existing caller on the unbounded path, which is a bound that exists and is
    /// not in force. The maximum is not checked here. It bounds what an operator
    /// may express rather than what this type can do, so it lives in
    /// <c>ConfigurationValidation</c> and a second copy of it here would be a
    /// second place that range is decided - the same split <c>ItemReader</c>
    /// already makes for the page size.
    /// </para>
    /// <para>
    /// The clock is taken rather than read, so a test arranges a pass that runs
    /// out of time with nothing running and no waiting, and the plugin holds no
    /// ambient clock anywhere.
    /// </para>
    /// </remarks>
    public Pass(Applier applier, IPassProgress progress, TimeProvider time, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(applier);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limit, TimeSpan.Zero);

        _applier = applier;
        _progress = progress;
        _time = time;
        _limit = limit;
    }

    /// <summary>
    /// Runs one pass over what the caller observed, continuing an interrupted
    /// pass over the same pairing where there is one.
    /// </summary>
    /// <param name="request">What the two servers hold, and what the operator asked for.</param>
    /// <param name="cancellationToken">Stops a pass an operator asked to stop.</param>
    /// <returns>What the pass did, and whether it reached the end of the plan.</returns>
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
        // Derived here rather than by the caller, and asked of the same route an
        // operator reads a plan through rather than derived a second time. What
        // a dry run showed and what this pass carries out are one object, so
        // they cannot disagree; two derivations that agree today are the
        // arrangement where the one nobody runs goes stale in silence.
        var intended = DryRun.Of(request, _progress);
        var plan = intended.Plan;
        var skipped = intended.ItemsAlreadyDone;

        var itemsWritten = 0;
        var fieldsWritten = 0;
        var itemsPassedOver = 0;
        var itemsDeferred = 0;
        var finished = true;

        // Read once, before the first item, and never again. A start re-read
        // inside the loop measures the last item instead of the pass, which is a
        // bound that can never be reached.
        var startedAt = _time.GetTimestamp();

        foreach (var item in plan.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Before the item and never during it. An item is the boundary this
            // pass can stop at, because the record that an item is finished with
            // is written after the applier returns from it; stopping anywhere
            // inside would be stopping at a point the resume has no name for.
            //
            // The bound is reached rather than exceeded: a pass that has used its
            // whole allowance stops instead of starting one more item on the
            // strength of having a fraction of a second left.
            if (_time.GetElapsedTime(startedAt) >= _limit)
            {
                finished = false;
                break;
            }

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

        // Cleared where the pass reached the end of the plan and nowhere else.
        // Cleared here rather than at the next pass's start: a record left behind
        // would make the next pass over this pairing skip every item this one
        // wrote.
        //
        // THE CONDITION IS THE HALF THIS METHOD IS EASIEST TO BREAK ON. A pass
        // stopped by the bound leaves through the same return as a pass that
        // finished, and routing it through this line as well would clear the
        // resume point of a pass that has not done the work - so the next pass
        // would start the library again with nothing saying so.
        if (finished)
        {
            _progress.Cleared(request.PairingId);
        }

        return new PassResult
        {
            ItemsWritten = itemsWritten,
            FieldsWritten = fieldsWritten,
            ItemsPassedOver = itemsPassedOver,
            ItemsDeferred = itemsDeferred,
            ItemsAlreadyDone = skipped,
            Finished = finished,
        };
    }
}
