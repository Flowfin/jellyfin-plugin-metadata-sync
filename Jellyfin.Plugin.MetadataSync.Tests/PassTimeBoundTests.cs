using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// A pass that reaches its time bound stops at an item boundary, keeps its
/// resume point and says it did not finish.
/// </summary>
/// <remarks>
/// The thing this suite is written against is not that the bound fires. It is
/// the line the bound is easiest to break while adding: a pass that runs to the
/// end clears the record of how far it got, and a stopped pass routed through
/// that same exit would clear the resume point of a pass that has not done the
/// work, so the pass after it would start the library again with nothing saying
/// so. <see cref="AStoppedPassKeepsItsResumePoint"/> is the case that fails when
/// the stopped path is routed through the clearing exit, and
/// <see cref="AFinishedPassStillClearsItsResumePoint"/> is the neighbour that
/// fails if the exit is never taken at all, so neither can be satisfied by a
/// pass that always says one thing.
/// <para>
/// The clock is an argument rather than an ambient reading, so every case here
/// runs with nothing waiting and no dependence on how fast the machine is. What
/// that costs is stated: these cases prove what a pass does with a clock's
/// answers and never that a real clock answers that way.
/// </para>
/// </remarks>
public class PassTimeBoundTests
{
    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");

    /// <summary>
    /// A pass whose clock passes the bound stops there, with the items it did
    /// not reach left in the plan, and says it did not finish.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task APassThatReachesTheBoundStopsAtAnItemBoundary()
    {
        var items = Items(5);
        var progress = new RecordingPassProgress();
        var target = new TargetThatStops(Guid.Empty);

        // One minute per reading, and three minutes allowed. The reading before
        // the loop is at zero, so the item boundaries are at one, two and three
        // minutes, and the third of them is where the pass has used its whole
        // allowance.
        var result = await new Pass(
                new Applier(target, new RecordingWrittenValues()),
                progress,
                new ClockThatAdvances(TimeSpan.FromMinutes(1)),
                TimeSpan.FromMinutes(3))
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.False(result.Finished);
        Assert.Equal(2, result.ItemsWritten);
        Assert.Equal(new[] { items[0], items[1] }, target.Written.Select(item => item.LocalItemId).ToArray());
    }

    /// <summary>
    /// The case the change exists for. A pass stopped by the bound keeps what it
    /// recorded, so the pass after it continues from there.
    /// </summary>
    /// <remarks>
    /// It goes red when the stopped path is routed through the exit that clears
    /// the record, which is the one-line mistake beside it: the clearing call
    /// moved out of its condition, or the condition written as something a
    /// stopped pass also satisfies.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AStoppedPassKeepsItsResumePoint()
    {
        var items = Items(5);
        var progress = new RecordingPassProgress();

        var result = await new Pass(
                new Applier(new TargetThatStops(Guid.Empty), new RecordingWrittenValues()),
                progress,
                new ClockThatAdvances(TimeSpan.FromMinutes(1)),
                TimeSpan.FromMinutes(3))
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.False(result.Finished);
        Assert.Equal(0, progress.Clearings);
        Assert.Equal(
            new[] { items[0], items[1] },
            progress.Recorded.Select(record => record.Item).ToArray());
    }

    /// <summary>
    /// And the pass after it covers the remainder rather than the whole, which
    /// is the resume point being kept read from the other end. A record that
    /// survived and was not read would satisfy the case above and fail here.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ThePassAfterAStoppedOneCoversTheRemainder()
    {
        var items = Items(5);
        var progress = new RecordingPassProgress();

        await new Pass(
                new Applier(new TargetThatStops(Guid.Empty), new RecordingWrittenValues()),
                progress,
                new ClockThatAdvances(TimeSpan.FromMinutes(1)),
                TimeSpan.FromMinutes(3))
            .RunAsync(RequestFor(items), CancellationToken.None);

        var afterwards = new TargetThatStops(Guid.Empty);
        var resumed = await new Pass(
                new Applier(afterwards, new RecordingWrittenValues()),
                progress,
                TimeProvider.System,
                PassClock.NotReached)
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.True(resumed.Finished);
        Assert.Equal(2, resumed.ItemsAlreadyDone);
        Assert.Equal(
            new[] { items[2], items[3], items[4] },
            afterwards.Written.Select(item => item.LocalItemId).ToArray());
    }

    /// <summary>
    /// The neighbour. A pass that finishes inside its bound still reports
    /// finished and still clears its resume point, so the answers above cannot
    /// be produced by a pass that never clears anything and never says it
    /// finished.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AFinishedPassStillClearsItsResumePoint()
    {
        var items = Items(5);
        var progress = new RecordingPassProgress();
        var target = new TargetThatStops(Guid.Empty);

        var result = await new Pass(
                new Applier(target, new RecordingWrittenValues()),
                progress,
                new ClockThatAdvances(TimeSpan.FromMinutes(1)),
                TimeSpan.FromMinutes(30))
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.True(result.Finished);
        Assert.Equal(5, result.ItemsWritten);
        Assert.Equal(1, progress.Clearings);
    }

    /// <summary>
    /// The bound measures the pass rather than the last item. A start re-read
    /// inside the loop is the one-line neighbour that turns this into a bound
    /// nothing can reach, and it is invisible in the result of any pass that
    /// finishes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task TheBoundMeasuresThePassAndNotTheLastItem()
    {
        var items = Items(5);
        var clock = new ClockThatAdvances(TimeSpan.FromMinutes(1));

        var result = await new Pass(
                new Applier(new TargetThatStops(Guid.Empty), new RecordingWrittenValues()),
                new RecordingPassProgress(),
                clock,
                TimeSpan.FromMinutes(3))
            .RunAsync(RequestFor(items), CancellationToken.None);

        // One reading before the loop and one at each boundary the pass reached,
        // which is three boundaries for a pass that stopped at the third.
        Assert.False(result.Finished);
        Assert.Equal(4, clock.Readings);
    }

    /// <summary>
    /// A pass over an empty plan finishes, whatever the clock says. The bound is
    /// asked before each item, so a plan with no items asks it never, and a pass
    /// with nothing to do that reported it had not finished would send every
    /// caller looking for work that does not exist.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task APassOverAnEmptyPlanFinishes()
    {
        var progress = new RecordingPassProgress();

        var result = await new Pass(
                new Applier(new TargetThatStops(Guid.Empty), new RecordingWrittenValues()),
                progress,
                new ClockThatAdvances(TimeSpan.FromDays(1)),
                TimeSpan.FromMinutes(1))
            .RunAsync(RequestFor(Array.Empty<Guid>()), CancellationToken.None);

        Assert.True(result.Finished);
        Assert.Equal(1, progress.Clearings);
    }

    /// <summary>
    /// A pass built with no clock is refused as it is built. An optional clock
    /// is how a bound arrives that reads the machine's own time instead of the
    /// one the caller chose, and the cases above could not then be written.
    /// </summary>
    [Fact]
    public void APassWithNoClockIsRefused()
    {
        Assert.Throws<ArgumentNullException>(
            () => new Pass(
                new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()),
                new RecordingPassProgress(),
                null!,
                PassClock.NotReached));
    }

    /// <summary>
    /// A pass allowed no time at all is refused as it is built. It is the lower
    /// end of the range, and it is refused here as well as in the configuration
    /// for the reason the page size's lower end is: a caller inside this plugin
    /// is not an operator and does not come through the configuration.
    /// </summary>
    /// <param name="minutes">A bound a caller might hand over.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void APassAllowedNoTimeIsRefused(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Pass(
                new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()),
                new RecordingPassProgress(),
                TimeProvider.System,
                TimeSpan.FromMinutes(minutes)));
    }

    /// <summary>
    /// The neighbour for the refusal above. The smallest bound the range admits
    /// is accepted, so the guard refuses a pass with no time rather than a short
    /// pass.
    /// </summary>
    [Fact]
    public void TheSmallestBoundTheRangeAdmitsIsAccepted()
    {
        _ = new Pass(
            new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()),
            new RecordingPassProgress(),
            TimeProvider.System,
            TimeSpan.FromMinutes(1));
    }

    private static List<Guid> Items(int count) =>
        Enumerable.Range(1, count)
            .Select(n => new Guid(string.Format(CultureInfo.InvariantCulture, "aaaaaaaa-0000-0000-0000-{0:D12}", n)))
            .ToList();

    private static PlanRequest RequestFor(IEnumerable<Guid> items)
    {
        var request = new PlanRequest
        {
            PairingId = _pairing,
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
                PeerValue = "what the peer said",
                LastWrittenByThisPlugin = null,
                FieldLockedHere = false,
                FieldLockedOnPeer = false,
            });

            request.Items.Add(item);
        }

        return request;
    }
}
