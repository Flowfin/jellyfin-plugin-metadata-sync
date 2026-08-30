using System;
using System.Collections.Generic;
using Jellyfin.Plugin.MetadataSync.Store;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// What a pass would do, derived with nothing present that could do it.
/// </summary>
/// <remarks>
/// A first release plans and does not write, and an operator reads what it would
/// change before anything changes. That is the whole of this type: it takes what
/// the two servers hold and hands back the plan, and there is no argument to it
/// that could carry a write.
/// <para>
/// THE PROPERTY IS REACHABILITY AND NOT INTENT. A route that planned by handing
/// an applier a plan and asking it politely not to write would read the same way
/// in a diff and would be one edit from writing. What is held instead is that
/// nothing an applier is made of is reachable from here at all, which the
/// suite asks of the compiled assembly rather than of this remark. That is
/// why this is a type of its own and not a second method on
/// <see cref="Pass"/>: a walk is seeded at a type, so a plan-only method
/// sitting beside a method that writes is inside the same subject and the
/// question cannot be put.
/// </para>
/// <para>
/// The plan is derived here and nowhere else, and <see cref="Pass"/> asks this
/// type for it rather than repeating the derivation. What an operator reads and
/// what an apply carries out are then the same object by construction. Two
/// derivations that agree today are the arrangement where a dry run goes on
/// describing a pass the tree has stopped making.
/// </para>
/// <para>
/// What it does not do. It does not read either server, which happens before a
/// request exists. It does not decide anything, which is
/// <see cref="Planner"/>'s. And it takes no clock, so a plan carries no age and
/// nothing here refuses one for being old: that is the fourth condition of #36
/// and it needs a number nobody has chosen.
/// </para>
/// </remarks>
public static class DryRun
{
    /// <summary>
    /// Derives what a pass over this request would do.
    /// </summary>
    /// <param name="request">What the two servers hold, and what the operator asked for.</param>
    /// <param name="progress">The record of how far an earlier pass over this pairing got.</param>
    /// <returns>The plan a pass would carry out, and what it would pass over as already done.</returns>
    /// <exception cref="ArgumentNullException">There is nothing to plan from, or nowhere to read how far an earlier pass got.</exception>
    /// <remarks>
    /// The record is required rather than optional, for the same reason the
    /// applier's store is on <see cref="Pass"/>. A dry run that could be taken
    /// without one would show an operator every item of a library an interrupted
    /// pass had already been through, and the apply beside it would then do
    /// something narrower than what they read.
    /// </remarks>
    public static DryRunResult Of(PlanRequest request, IPassProgress progress)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);

        // Read once, at the start, rather than per item. What an earlier pass
        // finished with cannot grow while this one is being derived, and a read
        // per item would make the answer depend on rows added underneath it.
        var alreadyDone = new HashSet<Guid>(progress.CompletedItems(request.PairingId));

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

        // Planned from what is left rather than from everything. A plan built
        // over the whole library and then filtered would have asked the conflict
        // rules about items this pass is not going to touch, and the plan an
        // operator reads would name changes the pass will not make.
        return new DryRunResult
        {
            Plan = Planner.Plan(remaining),
            ItemsAlreadyDone = skipped,
        };
    }
}
