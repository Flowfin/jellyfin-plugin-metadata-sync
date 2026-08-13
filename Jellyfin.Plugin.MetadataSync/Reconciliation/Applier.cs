using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// The half of a pass that acts, and it is deliberately dull.
/// </summary>
/// <remarks>
/// It takes a plan and obeys it. It holds no rule of its own: it never reads
/// the field register, never reads the direction, and never asks a conflict
/// rule anything, because every one of those was asked while the plan was being
/// made and asking again is how two halves of one pass come to disagree. The
/// suite refuses those names in this file rather than trusting this paragraph.
/// <para>
/// The one question it asks about an item is whether that item's plan writes
/// anything, and that is a boolean the plan carries. Given a plan that writes
/// nothing it calls nothing, which is the property that makes a dry run
/// provably a dry run.
/// </para>
/// <para>
/// What is not here, and is not hidden. The re-checks that belong to this half
/// are the ones that can have changed since the plan was made: whether the
/// resolution still holds, whether the pairing is still live, and whether
/// another component has written the item in the meantime. The first two need
/// the pairing contract, which this plugin does not reference. The third is
/// #41, and <c>docs/reconciliation.md</c> states both the mechanism and the
/// window it leaves open. This type applies a plan as it stands and claims
/// nothing about the time between the two halves.
/// </para>
/// <para>
/// One thing it does decide, and it decides it by type. An item the write path
/// hands back as deferred is counted and the pass carries on; anything else is
/// left to the caller. That is not a rule it reads from anywhere, it is the
/// difference between an event that happens on any library in use and a defect,
/// and #41 argues it where the deferral is raised.
/// </para>
/// <para>
/// A stopped pass stops within one item and reports nothing about how far it
/// got. That is the whole of what this type promises: it throws where the
/// operator asked it to stop, and turning that into a result carrying the
/// items already written is the pass's own bound, which is #37, and its
/// resumption, which is #38.
/// </para>
/// </remarks>
public sealed class Applier
{
    private readonly IPlanTarget _target;

    /// <summary>
    /// Initializes a new instance of the <see cref="Applier"/> class.
    /// </summary>
    /// <param name="target">The one route to the library.</param>
    /// <exception cref="ArgumentNullException">There is no target to write through.</exception>
    public Applier(IPlanTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _target = target;
    }

    /// <summary>
    /// Carries out a plan.
    /// </summary>
    /// <param name="plan">The plan, already decided.</param>
    /// <param name="cancellationToken">Stops a pass an operator asked to stop.</param>
    /// <returns>What was handed to the target.</returns>
    /// <exception cref="ArgumentNullException">There is no plan to apply.</exception>
    /// <remarks>
    /// The refusal is in this method and the work is in the one below it, so a
    /// caller with no plan is refused as it calls rather than whenever it gets
    /// round to awaiting. An argument check inside an asynchronous method is
    /// returned as a faulted task, which a caller that forgot to await never
    /// sees at all.
    /// </remarks>
    public Task<ApplyResult> ApplyAsync(Plan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return Apply(plan, cancellationToken);
    }

    private async Task<ApplyResult> Apply(Plan plan, CancellationToken cancellationToken)
    {
        var itemsWritten = 0;
        var fieldsWritten = 0;
        var itemsPassedOver = 0;
        var itemsDeferred = 0;

        foreach (var item in plan.Items)
        {
            // Asked here rather than only handed on. A token that reaches the
            // target still leaves the applier calling it once per item, so a
            // pass an operator stopped would keep starting work until the last
            // item in the plan. Asked at the top of the loop, a stopped pass
            // stops within one item, which is the number that can be stated.
            cancellationToken.ThrowIfCancellationRequested();

            if (!item.Writes)
            {
                itemsPassedOver++;
                continue;
            }

            // The one thing this half decides, and it decides it by type rather
            // than by asking anything. A deferral means the item moved or went
            // between the two halves of a pass, which is an ordinary event on a
            // library somebody uses, so the pass carries on and counts it. Every
            // other failure is a defect and is left to the caller, because a pass
            // that swallowed one would report a library as synced that is not.
            try
            {
                await _target.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            }
            catch (DeferredItemException)
            {
                itemsDeferred++;
                continue;
            }

            itemsWritten++;
            fieldsWritten += item.FieldsToWrite;
        }

        return new ApplyResult
        {
            ItemsWritten = itemsWritten,
            FieldsWritten = fieldsWritten,
            ItemsPassedOver = itemsPassedOver,
            ItemsDeferred = itemsDeferred,
        };
    }
}
