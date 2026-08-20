using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The number a write reports is the number it carried out.
/// </summary>
/// <remarks>
/// #57 asks that an action which writes confirm with a count, and that the count
/// match what the action then does. The confirmation itself has nothing to
/// travel on yet, because no request reaches the applier. What the tree can
/// already hold is the second half, and it is the half the failure lives in: a
/// surface that counts a plan and an action that counts again are about one
/// world only for as long as the two counts agree, and nothing said they had to.
///
/// The dishonest shape is not a lie somebody writes. It is a second count that
/// drifts. Counting the changes on an item rather than the changes that write,
/// counting an item the target refused, or dropping the items a plan passes over
/// each produce a number that is defensible on its own and is not the number an
/// operator was shown.
///
/// So the sweep runs the applier over every plan that can be built from five
/// kinds of item and asserts three things against what the target was actually
/// handed, rather than against a second reading of the plan. Every item is
/// accounted for exactly once. The items reported are the ones the target kept.
/// The fields reported are the fields those items carried.
///
/// What this does not cover, stated rather than left to be assumed. There is no
/// confirmation and no request, so nothing here says an action cannot run
/// without one, which is the rest of #57's first condition and its fourth. The
/// target is a stand-in, so what a server did with a write is outside this: the
/// result type says the same about itself. And a count shown on a page is not
/// yet a thing that exists to be compared against these.
/// </remarks>
public class ApplyAccountingTests
{
    /// <summary>
    /// The stamp the fixture target reads as an item that moved between the plan
    /// and the write.
    /// </summary>
    private const string Moved = "after it was planned";

    /// <summary>
    /// The kinds of item a plan can carry, as far as the applier's accounting is
    /// concerned. They differ in how many fields they carry, in how many of those
    /// they write, and in whether the target keeps them, so that a count of
    /// items, a count of fields considered and a count of fields written are
    /// three different numbers on the same plan and cannot be swapped for one
    /// another without being seen.
    /// </summary>
    private enum Kind
    {
        /// <summary>Writes one field, and the target keeps it.</summary>
        WritesOneField,

        /// <summary>Writes two fields, and the target keeps it.</summary>
        WritesTwoFields,

        /// <summary>Carries two changes and writes one of them, which is the
        /// only kind on which the fields an item carries and the fields it
        /// writes are different numbers.</summary>
        WritesOneOfTwoFields,

        /// <summary>Carries changes and writes none of them.</summary>
        WritesNothing,

        /// <summary>Writes two fields, and the target defers it because the
        /// item moved between the two halves of the pass.</summary>
        Deferred,
    }

    /// <summary>
    /// Gets the plans the sweep runs, as the arrangement each one is built from.
    /// Every arrangement of the kinds up to three items long, the empty plan
    /// included.
    /// </summary>
    public static TheoryData<string> Arrangements
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var arrangement in EveryArrangement())
            {
                data.Add(string.Join(",", arrangement));
            }

            return data;
        }
    }

    /// <summary>
    /// Every item in the plan is accounted for exactly once. An item that falls
    /// between the counts is the failure a total nobody adds up hides best.
    /// </summary>
    /// <param name="arrangement">The plan, as its kinds.</param>
    /// <returns>The running test.</returns>
    [Theory]
    [MemberData(nameof(Arrangements))]
    public async Task EveryItemIsCountedOnceAndUnderOneHeading(string arrangement)
    {
        var plan = PlanFor(arrangement);
        var target = new DeferringTarget();

        var result = await new Applier(target).ApplyAsync(plan, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(
            plan.Items.Count,
            result.ItemsWritten + result.ItemsPassedOver + result.ItemsDeferred);
    }

    /// <summary>
    /// The items reported are the ones the target kept, counted from what the
    /// target was handed rather than from the plan a second time.
    /// </summary>
    /// <param name="arrangement">The plan, as its kinds.</param>
    /// <returns>The running test.</returns>
    [Theory]
    [MemberData(nameof(Arrangements))]
    public async Task TheItemsReportedAreTheOnesTheTargetKept(string arrangement)
    {
        var plan = PlanFor(arrangement);
        var target = new DeferringTarget();

        var result = await new Applier(target).ApplyAsync(plan, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(target.Kept.Count, result.ItemsWritten);
        Assert.Equal(target.Deferred.Count, result.ItemsDeferred);
        Assert.All(target.Offered, item => Assert.True(item.Writes));
    }

    /// <summary>
    /// The fields reported are the fields the kept items carried, and never more
    /// than the plan said would be written. Equality with the plan's own number
    /// holds exactly where nothing was deferred, which is the case an operator
    /// is shown a number for.
    /// </summary>
    /// <param name="arrangement">The plan, as its kinds.</param>
    /// <returns>The running test.</returns>
    [Theory]
    [MemberData(nameof(Arrangements))]
    public async Task TheFieldsReportedAreTheFieldsThoseItemsCarried(string arrangement)
    {
        var plan = PlanFor(arrangement);
        var target = new DeferringTarget();

        var result = await new Applier(target).ApplyAsync(plan, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(target.Kept.Sum(item => item.FieldsToWrite), result.FieldsWritten);
        Assert.True(result.FieldsWritten <= plan.FieldsToWrite);
        Assert.Equal(result.ItemsDeferred == 0, result.FieldsWritten == plan.FieldsToWrite);
    }

    /// <summary>
    /// The sweep reaches every arm it claims to. A sweep that never defers, or
    /// never passes an item over, would leave two of the three headings above
    /// asserted only against zero, and a long run that proves nothing looks
    /// exactly like one that proves everything. The last of the four is the one
    /// the first version of this sweep did not reach: with no kept item carrying
    /// a field it passes over, an applier counting the changes considered
    /// instead of the changes written answers correctly on every plan.
    /// </summary>
    [Fact]
    public async Task TheSweepReachesEveryArmOfTheAccounting()
    {
        var written = 0;
        var passedOver = 0;
        var deferred = 0;
        var plans = 0;
        var fieldsPassedOverOnKeptItems = 0;

        foreach (var arrangement in EveryArrangement())
        {
            var target = new DeferringTarget();
            var result = await new Applier(target)
                .ApplyAsync(PlanFor(string.Join(",", arrangement)), CancellationToken.None)
                .ConfigureAwait(true);

            plans++;
            fieldsPassedOverOnKeptItems += target.Kept.Sum(item => item.Changes.Count - item.FieldsToWrite);
            written += result.ItemsWritten;
            passedOver += result.ItemsPassedOver;
            deferred += result.ItemsDeferred;
        }

        var kinds = Enum.GetValues<Kind>().Length;
        Assert.Equal(1 + kinds + (kinds * kinds) + (kinds * kinds * kinds), plans);
        Assert.True(written > 0);
        Assert.True(passedOver > 0);
        Assert.True(deferred > 0);
        Assert.True(fieldsPassedOverOnKeptItems > 0);
    }

    /// <summary>
    /// Every arrangement of the four kinds up to three items long.
    /// </summary>
    /// <returns>The arrangements, shortest first.</returns>
    private static IEnumerable<IReadOnlyList<Kind>> EveryArrangement()
    {
        var kinds = Enum.GetValues<Kind>();
        IReadOnlyList<IReadOnlyList<Kind>> current = new[] { Array.Empty<Kind>() };

        yield return Array.Empty<Kind>();

        for (var length = 1; length <= 3; length++)
        {
            var next = new List<IReadOnlyList<Kind>>();

            foreach (var shorter in current)
            {
                foreach (var kind in kinds)
                {
                    var longer = shorter.Append(kind).ToList();
                    next.Add(longer);
                    yield return longer;
                }
            }

            current = next;
        }
    }

    /// <summary>
    /// Builds the plan an arrangement names.
    /// </summary>
    /// <param name="arrangement">The kinds, comma separated, possibly empty.</param>
    /// <returns>The plan.</returns>
    private static Plan PlanFor(string arrangement)
    {
        var plan = new Plan();

        if (arrangement.Length == 0)
        {
            return plan;
        }

        foreach (var name in arrangement.Split(','))
        {
            plan.Items.Add(ItemOf(Enum.Parse<Kind>(name)));
        }

        return plan;
    }

    /// <summary>
    /// Builds one item of a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The item.</returns>
    private static ItemPlan ItemOf(Kind kind)
    {
        var item = new ItemPlan
        {
            Kind = "Movie",
            LastSavedWhenPlanned = kind == Kind.Deferred ? Moved : "when it was planned",
        };

        item.Changes.Add(Change("Overview", writes: kind != Kind.WritesNothing));

        switch (kind)
        {
            case Kind.WritesTwoFields:
            case Kind.Deferred:
                item.Changes.Add(Change("Tagline", writes: true));
                break;

            case Kind.WritesOneOfTwoFields:
            case Kind.WritesNothing:
                item.Changes.Add(Change("Tagline", writes: false));
                break;

            default:
                break;
        }

        return item;
    }

    /// <summary>
    /// Builds one planned change.
    /// </summary>
    /// <param name="field">The field it is about.</param>
    /// <param name="writes">Whether it writes.</param>
    /// <returns>The change.</returns>
    private static PlannedChange Change(string field, bool writes)
    {
        return new PlannedChange
        {
            Field = field,
            PeerValue = "theirs",
            Writes = writes,
            ValueToWrite = writes ? "theirs" : null,
        };
    }

    /// <summary>
    /// A target that keeps what it is handed and defers an item whose last-saved
    /// stamp says it moved, so one plan can mix the two. The two kinds that
    /// write two fields are told apart by the stamp rather than by the count, so
    /// a deferred item and a kept one of the same size sit side by side.
    /// </summary>
    /// <remarks>
    /// It is written here rather than added to <c>RecordingPlanTarget</c>
    /// because that fixture is what several other classes mean by a target that
    /// does nothing, and a target that sometimes throws is a different thing to
    /// be handed by accident.
    /// </remarks>
    private sealed class DeferringTarget : IPlanTarget
    {
        /// <summary>Gets every item the applier offered, in order.</summary>
        public Collection<ItemPlan> Offered { get; } = new();

        /// <summary>Gets the items this target accepted.</summary>
        public Collection<ItemPlan> Kept { get; } = new();

        /// <summary>Gets the items this target refused as deferred.</summary>
        public Collection<ItemPlan> Deferred { get; } = new();

        /// <inheritdoc />
        public Task WriteAsync(ItemPlan item, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(item);
            Offered.Add(item);

            if (string.Equals(item.LastSavedWhenPlanned, Moved, StringComparison.Ordinal))
            {
                Deferred.Add(item);
                throw new ItemChangedSincePlannedException("The fixture defers this item.");
            }

            Kept.Add(item);
            return Task.CompletedTask;
        }
    }
}
