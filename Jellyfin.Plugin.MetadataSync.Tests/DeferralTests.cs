using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using MediaBrowser.Controller.Entities.Movies;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The window between deciding and writing, driven end to end: an item is read,
/// a plan is made from it, something else writes the item, and the plan is
/// carried out.
/// </summary>
/// <remarks>
/// This runs the three halves together on purpose. Every other file here holds
/// one of them still and asks what it does; what an operator meets is the
/// sequence, and the defect this is about only exists between two of the halves.
/// <para>
/// What it is not is a concurrency test, and the difference matters enough to
/// say before the first assertion. The write that moves the item happens between
/// two statements on one thread, so this proves the comparison and the counting,
/// and it proves nothing about a real race. The residual is a property of the
/// design rather than of the test: there is no lock this plugin can take across
/// another component's write, the interval between the comparison and the write
/// stays open, and <c>docs/reconciliation.md</c> says so rather than claiming it
/// away.
/// </para>
/// </remarks>
public class DeferralTests
{
    private static readonly Guid _moved = new("44444444-4444-4444-4444-444444444444");
    private static readonly Guid _untouched = new("55555555-5555-5555-5555-555555555555");

    /// <summary>
    /// When something else saves one item between the plan and the write, that
    /// item is deferred, nothing on it is written, and the rest of the pass
    /// carries on.
    /// </summary>
    /// <remarks>
    /// The second item is what makes this worth running. A pass that fell over
    /// on the first deferral would satisfy every assertion about the first item
    /// and would leave a library half synced with one refresh.
    /// </remarks>
    [Fact]
    public async Task AnItemSomethingElseWroteIsDeferredAndTheRestOfThePassGoesOn()
    {
        var moved = new Movie { Overview = "what this server holds" };
        var untouched = new Movie { Overview = "what this server holds" };

        var (library, calls) = LibraryCalls.Empty();
        calls.Items[_moved] = moved;
        calls.Items[_untouched] = untouched;

        var plan = Planner.Plan(RequestFor(
            Reading(_moved, moved),
            Reading(_untouched, untouched)));

        // Something else saves one of the two. On a server this is a library
        // scan, a provider refresh or an operator editing the same item.
        moved.Overview = "what a refresh just wrote";
        moved.DateLastSaved = new DateTime(2026, 8, 13, 1, 0, 0, DateTimeKind.Utc);

        var result = await new Applier(new LibraryPlanTarget(library), new RecordingWrittenValues()).ApplyAsync(plan, CancellationToken.None);

        Assert.Equal(1, result.ItemsDeferred);
        Assert.Equal(1, result.ItemsWritten);
        Assert.Equal(0, result.ItemsPassedOver);

        Assert.Equal("what a refresh just wrote", moved.Overview);
        Assert.Equal("theirs", untouched.Overview);

        Assert.Same(untouched, Assert.Single(calls.Updates).Item);
    }

    /// <summary>
    /// An item that has gone between the plan and the write is deferred on the
    /// same footing. It is the other event that is nobody's defect, and a pass
    /// that failed on it would fail every time an operator removed a film while
    /// a sync ran.
    /// </summary>
    [Fact]
    public async Task AnItemThatHasGoneIsDeferredOnTheSameFooting()
    {
        var going = new Movie { Overview = "what this server holds" };

        var (library, calls) = LibraryCalls.Holding(_moved, going);
        var plan = Planner.Plan(RequestFor(Reading(_moved, going)));

        calls.Items.Remove(_moved);

        var result = await new Applier(new LibraryPlanTarget(library), new RecordingWrittenValues()).ApplyAsync(plan, CancellationToken.None);

        Assert.Equal(1, result.ItemsDeferred);
        Assert.Equal(0, result.ItemsWritten);
        Assert.Empty(calls.Updates);
    }

    /// <summary>
    /// The neighbour, and the one that keeps the two above from meaning that a
    /// pass swallows whatever goes wrong. A plan row that does not describe a
    /// value is a defect in whatever produced it, and it stops the pass rather
    /// than being counted and passed over.
    /// </summary>
    [Fact]
    public async Task ADefectInAPlanRowStopsThePassRatherThanBeingDeferred()
    {
        var movie = new Movie();
        var (library, calls) = LibraryCalls.Holding(_moved, movie);

        var plan = new Plan();
        var item = new ItemPlan
        {
            LocalItemId = _moved,
            Kind = "Movie",
            LastSavedWhenPlanned = LibraryPlanTarget.StampOf(movie),
        };

        item.Changes.Add(new PlannedChange
        {
            Field = "ProductionYear",
            PeerValue = "nineteen seventy nine",
            Writes = true,
            ValueToWrite = "nineteen seventy nine",
            Reason = "arranged in a test",
        });

        plan.Items.Add(item);

        await Assert.ThrowsAsync<WriteRefusedException>(
            () => new Applier(new LibraryPlanTarget(library), new RecordingWrittenValues()).ApplyAsync(plan, CancellationToken.None));

        Assert.Empty(calls.Updates);
    }

    /// <summary>
    /// The whole arrangement writes when nothing moves. Without this the two
    /// tests above would pass against a plugin that never writes anything, which
    /// is the shape a deferral guard fails into.
    /// </summary>
    [Fact]
    public async Task NothingIsDeferredWhenNothingMoved()
    {
        var movie = new Movie { Overview = "what this server holds" };
        var (library, calls) = LibraryCalls.Holding(_moved, movie);

        var plan = Planner.Plan(RequestFor(Reading(_moved, movie)));

        var result = await new Applier(new LibraryPlanTarget(library), new RecordingWrittenValues()).ApplyAsync(plan, CancellationToken.None);

        Assert.Equal(0, result.ItemsDeferred);
        Assert.Equal(1, result.ItemsWritten);
        Assert.Equal("theirs", movie.Overview);
        Assert.Single(calls.Updates);
    }

    /// <summary>
    /// One item as the two servers hold it, with the token this server's copy
    /// answers with at the moment it was read.
    /// </summary>
    private static ItemObservation Reading(Guid id, Movie asHeldHere)
    {
        var observation = new ItemObservation
        {
            LocalItemId = id,
            PeerItemId = id,
            Kind = "Movie",
            LastSavedHere = LibraryPlanTarget.StampOf(asHeldHere),
        };

        observation.Fields.Add(new FieldObservation
        {
            Field = "Overview",
            LocalValue = null,
            PeerValue = "theirs",
            LastWrittenByThisPlugin = null,
            FieldLockedHere = false,
            FieldLockedOnPeer = false,
        });

        return observation;
    }

    private static PlanRequest RequestFor(params ItemObservation[] items)
    {
        var request = new PlanRequest();

        foreach (var item in items)
        {
            request.Items.Add(item);
        }

        return request;
    }
}
