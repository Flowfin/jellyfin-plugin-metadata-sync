using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// A pass that was stopped is continued rather than started again, and it is
/// continued by re-deriving rather than by replaying.
/// </summary>
/// <remarks>
/// The interruption is the thing a test cannot do to itself, so it is stood in
/// for: a target or a progress record that throws where the process would have
/// died leaves both of them holding exactly what they would have been holding,
/// and the resume is then asked about that state. What is asserted is the union
/// of the two runs, because a resume that covered the remainder and a resume
/// that covered everything are indistinguishable from the second run alone.
/// <para>
/// The three boundaries in <see cref="StoppedAt"/> are the whole of what a
/// pass's loop has around one item, and one of them is deliberately left open:
/// an item written and not yet recorded is written again by the resume. That is
/// asserted rather than argued away, because the ordering that closes it opens a
/// worse one, and a suite that only asserted the good half would be describing a
/// mechanism this one does not have.
/// </para>
/// </remarks>
public class PassResumptionTests
{
    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");

    private static readonly string[] _whatThePeerSaysNow = { "what the peer says now", "what the peer says now" };

    /// <summary>
    /// Gets one case per boundary and per position in a three-item pass, so
    /// every stage a pass can be killed at is killed at.
    /// </summary>
    public static TheoryData<string, int> EveryBoundary
    {
        get
        {
            var cases = new TheoryData<string, int>();

            foreach (var stage in Enum.GetValues<StoppedAt>())
            {
                for (var item = 0; item < 3; item++)
                {
                    cases.Add(stage.ToString(), item);
                }
            }

            return cases;
        }
    }

    /// <summary>
    /// A pass killed at any boundary of any item leaves a state the pass after
    /// it continues from: every item is written, none of them is written twice,
    /// except the one boundary where writing twice is the price of the ordering.
    /// </summary>
    /// <param name="boundary">Where the first pass was stopped, named as <see cref="StoppedAt"/> spells it.</param>
    /// <param name="position">Which item it was stopped at.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(EveryBoundary))]
    public async Task KilledAtAnyBoundaryTheWorkIsCoveredExactlyOnce(string boundary, int position)
    {
        var stage = Enum.Parse<StoppedAt>(boundary);
        var items = Items(3);
        var at = items[position];
        var progress = Stopping(stage, at);

        var first = stage == StoppedAt.TheWrite ? new TargetThatStops(at) : new TargetThatStops(Guid.Empty);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new Pass(new Applier(first, new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
                .RunAsync(RequestFor(items), CancellationToken.None));

        var second = new TargetThatStops(Guid.Empty);
        var resumed = await new Pass(new Applier(second, new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items), CancellationToken.None);

        var written = first.Written.Concat(second.Written).Select(item => item.LocalItemId).ToList();

        // Every item reached a write, which is the half a resume exists for.
        Assert.Equal(items.Order().ToList(), written.Distinct().Order().ToList());

        // And the count says which boundary was crossed. An item written and not
        // yet recorded is written again; every other stage costs nothing.
        Assert.Equal(
            stage == StoppedAt.AfterTheWriteBeforeTheRecord ? items.Count + 1 : items.Count,
            written.Count);

        Assert.Equal(position + (stage == StoppedAt.AfterTheRecord ? 1 : 0), resumed.ItemsAlreadyDone);
    }

    /// <summary>
    /// The resume covers the remainder rather than the whole, which is the
    /// condition read from the other end: the second run is asserted on its own,
    /// so a resume that quietly did everything again fails here even though the
    /// union above would be satisfied.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AResumedPassCoversExactlyTheRemainder()
    {
        var items = Items(4);
        var progress = new ProgressThatStops(items[2], afterRecording: true);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new Pass(new Applier(new TargetThatStops(Guid.Empty), new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
                .RunAsync(RequestFor(items), CancellationToken.None));

        var target = new TargetThatStops(Guid.Empty);
        var resumed = await new Pass(new Applier(target, new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.Equal(new[] { items[3] }, target.Written.Select(item => item.LocalItemId).ToArray());
        Assert.Equal(3, resumed.ItemsAlreadyDone);
        Assert.Equal(1, resumed.ItemsWritten);
    }

    /// <summary>
    /// A pass that runs to the end leaves nothing for the next one to skip. It
    /// is the leg that reddens when the clearing at the end of a pass is
    /// removed, and without it a plugin would sync a library once and then never
    /// again.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ThePassAfterAFinishedOneConsidersEveryItemAgain()
    {
        var items = Items(3);
        var progress = new RecordingPassProgress();

        var first = new TargetThatStops(Guid.Empty);
        await new Pass(new Applier(first, new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items), CancellationToken.None);

        var second = new TargetThatStops(Guid.Empty);
        var again = await new Pass(new Applier(second, new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.Equal(3, first.Written.Count);
        Assert.Equal(3, second.Written.Count);
        Assert.Equal(0, again.ItemsAlreadyDone);
        Assert.Empty(progress.CompletedItems(_pairing));
    }

    /// <summary>
    /// The clearing happens at the end of a pass and not at its start. Clearing
    /// on the way in would satisfy the leg above and lose every resume, so the
    /// two are separated by asserting that an interrupted pass left its record
    /// behind.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AnInterruptedPassLeavesItsRecordBehind()
    {
        var items = Items(3);
        var progress = new ProgressThatStops(items[1], afterRecording: true);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new Pass(new Applier(new TargetThatStops(Guid.Empty), new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
                .RunAsync(RequestFor(items), CancellationToken.None));

        Assert.Equal(new[] { items[0], items[1] }.Order().ToList(), progress.CompletedItems(_pairing).Order().ToList());
        Assert.Equal(0, progress.Clearings);
    }

    /// <summary>
    /// The resume writes what the peer holds when it runs. The peer's value for
    /// an item the first pass never reached is changed between the two runs, and
    /// the value that arrives at the library is the new one, which is what says
    /// the plan was built again rather than carried over.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AResumedPassWritesWhatThePeerHoldsNow()
    {
        var items = Items(3);
        var progress = new ProgressThatStops(items[0], afterRecording: true);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new Pass(new Applier(new TargetThatStops(Guid.Empty), new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
                .RunAsync(RequestFor(items), CancellationToken.None));

        var target = new TargetThatStops(Guid.Empty);
        var written = new RecordingWrittenValues();

        await new Pass(new Applier(target, written), progress, TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items, peerValue: "what the peer says now"), CancellationToken.None);

        Assert.Equal(_whatThePeerSaysNow, written.Recorded.Select(record => record.Value).ToArray());
    }

    /// <summary>
    /// Nothing this plugin persists is made of a plan. It is the guard behind
    /// the sentence that a pass resumes rather than replays: a store that could
    /// hold a plan is a store somebody can replay one out of, and the difference
    /// between the two mechanisms is invisible from the outside until a peer
    /// value changes under an interruption.
    /// </summary>
    [Fact]
    public void NoStoreInThisPluginIsMadeOfAPlan()
    {
        var refused = StoreMembers()
            .Where(member => MadeOf(member.Type).Any(IsAPlan))
            .Select(member => member.Site)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(refused);
    }

    /// <summary>
    /// The bite for the guard above, which without it passes on a tree where
    /// every store holds a plan and the walk is broken. The three types a plan
    /// is made of are offered directly and through a collection, because a plan
    /// kept per item arrives as the second.
    /// </summary>
    /// <param name="offered">A type a store member might have.</param>
    [Theory]
    [InlineData(typeof(Plan))]
    [InlineData(typeof(ItemPlan))]
    [InlineData(typeof(PlannedChange))]
    [InlineData(typeof(List<ItemPlan>))]
    [InlineData(typeof(Dictionary<Guid, Plan>))]
    public void APlanIsRefusedByItsShape(Type offered)
    {
        Assert.NotEmpty(MadeOf(offered).Where(IsAPlan).ToList());
    }

    /// <summary>
    /// The neighbour, so the guard is not one that refuses everything. What a
    /// progress record is actually made of sits one type argument away from the
    /// collection of item plans refused above.
    /// </summary>
    /// <param name="offered">A type a store member might have.</param>
    [Theory]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(HashSet<Guid>))]
    [InlineData(typeof(Dictionary<Guid, HashSet<Guid>>))]
    public void WhatAProgressRecordIsMadeOfIsAccepted(Type offered)
    {
        Assert.Empty(MadeOf(offered).Where(IsAPlan).ToList());
    }

    /// <summary>
    /// An item a pass considered and had nothing to write to is finished with,
    /// so a resume does not consider it again. It is the half of the record that
    /// costs a line per item on a library that changes little, which
    /// <c>docs/storage.md</c> states.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AnItemWithNothingToWriteIsRecordedAsFinishedWith()
    {
        var items = Items(1);
        var progress = new RecordingPassProgress();

        var result = await new Pass(new Applier(new TargetThatStops(Guid.Empty), new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items, peerValue: null), CancellationToken.None);

        Assert.Equal(1, result.ItemsPassedOver);
        Assert.Equal(0, result.ItemsWritten);
        Assert.Equal(new[] { (_pairing, items[0]) }, progress.Recorded.ToArray());
    }

    /// <summary>
    /// A deferred item is not finished with, so the pass that runs next reaches
    /// it again. The difference between this and the case above is the whole
    /// reason the record is written from the applier's answer rather than from
    /// the loop having got to the end of an item.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ADeferredItemIsNotRecordedAsFinishedWith()
    {
        var items = Items(1);
        var progress = new RecordingPassProgress();

        var result = await new Pass(new Applier(new DeferringPlanTarget(), new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.Equal(1, result.ItemsDeferred);
        Assert.Empty(progress.Recorded);
    }

    /// <summary>
    /// What an operator excluded reaches the plan the pass builds. The pass
    /// rebuilds the request for what is left, and a rebuild that dropped the
    /// exclusions would write a field the operator had turned off - which is
    /// worse under a resume than without one, because it would happen only to
    /// the items an interruption left over.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task WhatTheOperatorExcludedIsStillExcludedAfterAResume()
    {
        var items = Items(1);
        var progress = new RecordingPassProgress();
        var target = new TargetThatStops(Guid.Empty);

        var request = RequestFor(items);
        request.ExcludedFields.Add("Overview");

        var result = await new Pass(new Applier(target, new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
            .RunAsync(request, CancellationToken.None);

        Assert.Empty(target.Written);
        Assert.Equal(1, result.ItemsPassedOver);
    }

    /// <summary>
    /// A pass with nowhere to write is refused as it is built.
    /// </summary>
    [Fact]
    public void APassWithNoApplierIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new Pass(null!, new RecordingPassProgress(), TimeProvider.System, PassClock.NotReached));
    }

    /// <summary>
    /// A pass with nowhere to record how far it got is refused as it is built.
    /// An optional record is the way a pass arrives that loses everything at an
    /// interruption.
    /// </summary>
    [Fact]
    public void APassWithNowhereToRecordProgressIsRefused()
    {
        Assert.Throws<ArgumentNullException>(
            () => new Pass(new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()), null!, TimeProvider.System, PassClock.NotReached));
    }

    /// <summary>
    /// A pass with no request is refused as the caller calls, rather than
    /// whenever it gets round to awaiting.
    /// </summary>
    [Fact]
    public void RunningAPassWithNoRequestIsRefused()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
            {
                _ = new Pass(new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()), new RecordingPassProgress(), TimeProvider.System, PassClock.NotReached)
                    .RunAsync(null!, CancellationToken.None);
            });
    }

    /// <summary>
    /// A pass an operator stopped stops before it takes an item, and it leaves
    /// nothing recorded for an item it did not reach.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ACancelledPassStopsBeforeTheItem()
    {
        var items = Items(2);
        var progress = new RecordingPassProgress();
        var target = new TargetThatStops(Guid.Empty);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new Pass(new Applier(target, new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
                .RunAsync(RequestFor(items), new CancellationToken(canceled: true)));

        Assert.Empty(target.Written);
        Assert.Empty(progress.Recorded);
        Assert.Equal(0, progress.Clearings);
    }

    /// <summary>
    /// The neighbour for the two refusals above and for the cancellation: an
    /// ordinary pass over ordinary items writes them and records them.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AnOrdinaryPassWritesEveryItemAndRecordsEachOne()
    {
        var items = Items(2);
        var progress = new RecordingPassProgress();
        var target = new TargetThatStops(Guid.Empty);

        var result = await new Pass(new Applier(target, new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.Equal(items, target.Written.Select(item => item.LocalItemId).ToList());
        Assert.Equal(2, result.ItemsWritten);
        Assert.Equal(2, result.FieldsWritten);
        Assert.Equal(1, progress.Clearings);
    }

    /// <summary>
    /// The record a pass reads is the one for its own pairing. Two pairings
    /// interrupted at once are two resumes, and a record read across them would
    /// make one pairing's pass skip the other's items.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task OnePairingsProgressIsNotAnotherPairingsProgress()
    {
        var items = Items(2);
        var progress = new ProgressThatStops(items[0], afterRecording: true);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new Pass(new Applier(new TargetThatStops(Guid.Empty), new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
                .RunAsync(RequestFor(items), CancellationToken.None));

        var target = new TargetThatStops(Guid.Empty);
        var other = new Guid("dddddddd-0000-0000-0000-000000000004");

        var elsewhere = await new Pass(new Applier(target, new RecordingWrittenValues()), progress, TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items, pairing: other), CancellationToken.None);

        Assert.Equal(0, elsewhere.ItemsAlreadyDone);
        Assert.Equal(2, target.Written.Count);
    }

    private static RecordingPassProgress Stopping(StoppedAt stage, Guid at) => stage switch
    {
        StoppedAt.TheWrite => new RecordingPassProgress(),
        StoppedAt.AfterTheWriteBeforeTheRecord => new ProgressThatStops(at, afterRecording: false),
        _ => new ProgressThatStops(at, afterRecording: true),
    };

    private static List<Guid> Items(int count) =>
        Enumerable.Range(1, count)
            .Select(n => new Guid(string.Format(CultureInfo.InvariantCulture, "aaaaaaaa-0000-0000-0000-{0:D12}", n)))
            .ToList();

    private static PlanRequest RequestFor(
        IEnumerable<Guid> items,
        string? peerValue = "what the peer said",
        Guid? pairing = null)
    {
        var request = new PlanRequest
        {
            PairingId = pairing ?? _pairing,
            Direction = SyncDirection.TwoWay,
        };

        foreach (var id in items)
        {
            var item = new ItemObservation
            {
                LocalItemId = id,
                PeerItemId = id,
                Kind = "Movie",
                ItemLockedHere = false,
            };

            item.Fields.Add(new FieldObservation
            {
                Field = "Overview",
                LocalValue = null,
                PeerValue = peerValue,
                LastWrittenByThisPlugin = null,
                FieldLockedHere = false,
                FieldLockedOnPeer = false,
            });

            request.Items.Add(item);
        }

        return request;
    }

    private static bool IsAPlan(string name) =>
        name is "Jellyfin.Plugin.MetadataSync.Reconciliation.Plan"
             or "Jellyfin.Plugin.MetadataSync.Reconciliation.ItemPlan"
             or "Jellyfin.Plugin.MetadataSync.Reconciliation.PlannedChange";

    /// <summary>
    /// Every type a type is made of, following type arguments, so a plan held
    /// inside a collection or a dictionary is reached rather than passing under
    /// the collection's own name.
    /// </summary>
    /// <param name="type">The type to read.</param>
    /// <returns>The names of the types it is made of, its own included.</returns>
    private static IEnumerable<string> MadeOf(Type type)
    {
        yield return type.FullName ?? type.Name;

        if (type.IsArray && type.GetElementType() is { } element)
        {
            foreach (var name in MadeOf(element))
            {
                yield return name;
            }
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var name in MadeOf(argument))
            {
                yield return name;
            }
        }
    }

    /// <summary>
    /// Every member of every store this plugin declares, found rather than
    /// listed, so a store added later is walked without anybody editing this.
    /// </summary>
    /// <returns>The members, each with the site that names it.</returns>
    private static IEnumerable<(string Site, Type Type)> StoreMembers()
    {
        var stores = typeof(Plugin).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IPairingStore).IsAssignableFrom(type))
            .SelectMany(type => type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Append(type));

        const BindingFlags Everything =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var store in stores)
        {
            foreach (var field in store.GetFields(Everything))
            {
                yield return (store.Name + "." + field.Name, field.FieldType);
            }

            foreach (var property in store.GetProperties(Everything))
            {
                yield return (store.Name + "." + property.Name, property.PropertyType);
            }
        }
    }

    /// <summary>
    /// A write path that defers everything, so the case about a deferral is
    /// about the pass rather than about a library.
    /// </summary>
    private sealed class DeferringPlanTarget : IPlanTarget
    {
        public Task WriteAsync(ItemPlan item, CancellationToken cancellationToken)
        {
            throw new ItemChangedSincePlannedException("Something else wrote this item between the two halves of the pass.");
        }
    }
}
