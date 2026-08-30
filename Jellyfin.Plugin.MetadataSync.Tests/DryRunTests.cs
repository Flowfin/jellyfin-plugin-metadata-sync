using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using MediaBrowser.Controller.Entities;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// A plan can be had without anything that could write one out.
/// </summary>
/// <remarks>
/// Decision 8 in #1 is that the first release plans and does not write, so the
/// plan-only route is not a safety net in front of the release, it is what the
/// release does. What that asks of the tree is not that a route chooses not to
/// write - a route holding an applier and declining to use it reads the same way
/// in a diff and is one edit from writing - but that nothing a write is made of
/// is reachable from it at all.
/// <para>
/// That is a question about the call graph rather than about names, and it can
/// only be put to a type. A walk is seeded at a type and reads every method on
/// it, so a plan-only method sitting beside a method that applies is inside one
/// subject and the two cannot be told apart. <see cref="DryRun"/> exists as a
/// type of its own for that reason, and the first leg below is the reason.
/// </para>
/// <para>
/// The behavioural half is beside it and is not the same claim. A route that
/// reached no applier and derived a different plan from the one a pass carries
/// out would pass every leg above and would show an operator a document about
/// some other pass. So the plan is asserted against what the write path is
/// actually handed, over one arrangement, rather than against a second
/// expectation written down here.
/// </para>
/// <para>
/// The bound of the walk is <see cref="AssemblyReachability"/>'s own and is
/// stated at that type. What matters here: a member reached by reflection from
/// a string spells no token and is invisible, and the walk stops at the edge of
/// this assembly.
/// </para>
/// </remarks>
public class DryRunTests
{
    private static readonly Guid _pairing = new("dddddddd-0000-0000-0000-000000000004");

    /// <summary>
    /// The types a write is made of. The half of a pass that applies, the
    /// interface it writes through, the implementation behind that interface,
    /// the record it keeps of what it wrote, and the server's library and item
    /// underneath all of them.
    /// </summary>
    private static readonly string[] WhatAWriteIsMadeOf =
    {
        "Jellyfin.Plugin.MetadataSync.Reconciliation.Applier",
        "Jellyfin.Plugin.MetadataSync.Reconciliation.IPlanTarget",
        "Jellyfin.Plugin.MetadataSync.Reconciliation.LibraryPlanTarget",
        "Jellyfin.Plugin.MetadataSync.Store.IWrittenValues",
        "MediaBrowser.Controller.Library.ILibraryManager",
        "MediaBrowser.Controller.Entities.BaseItem",
    };

    /// <summary>
    /// The rule. Nothing a dry run reaches is a way to write.
    /// </summary>
    [Fact]
    public void NothingAWriteIsMadeOfIsReachableFromADryRun()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnThePlanOnlyPath);

        Assert.Empty(reached.TypesAmong(WhatAWriteIsMadeOf));
    }

    /// <summary>
    /// The walk starts somewhere. A predicate matching no type reaches nothing
    /// and passes the rule above on any tree at all, which is how this guard
    /// would go quiet the day the plan-only route is renamed.
    /// </summary>
    [Fact]
    public void TheWalkStartsFromThePlanOnlyRouteThatIsInTheTree()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnThePlanOnlyPath);

        Assert.Contains(typeof(DryRun).FullName!, reached.EntryTypes, StringComparer.Ordinal);
    }

    /// <summary>
    /// It decodes instructions rather than none. A walk that read no method body
    /// at all reaches nothing and is indistinguishable from one that read every
    /// body and found nothing.
    /// </summary>
    [Fact]
    public void TheWalkReadsInstructionsRatherThanNone()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnThePlanOnlyPath);

        Assert.True(reached.MethodsRead > 0, "The walk decoded no method bodies, so it can refuse nothing.");
    }

    /// <summary>
    /// It follows the route several types past its first call, so the rule
    /// above is not passing because the walk stopped early. The declarations a
    /// plan is decided against are read out of documents this plugin carries,
    /// and the reader of those is three types away from a dry run: through the
    /// planner, into the field register and the conflict rules, and out to the
    /// serialiser that parses them.
    /// </summary>
    [Fact]
    public void TheWalkFollowsTheRoutePastItsFirstCall()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnThePlanOnlyPath);

        Assert.Contains("System.Text.Json.JsonSerializer", reached.Types, StringComparer.Ordinal);
    }

    /// <summary>
    /// And the leg that says this is not a name scan under another name. The
    /// same assembly reaches every one of those types from a pass, and the dry
    /// run is not refused for the company it keeps in one namespace.
    /// </summary>
    [Fact]
    public void TheWalkDoesNotRefuseAWriteItCannotReach()
    {
        var fromAPass = AssemblyReachability.From(
            typeof(Plugin).Assembly,
            name => string.Equals(name, typeof(Pass).FullName, StringComparison.Ordinal));

        Assert.NotEmpty(fromAPass.TypesAmong(WhatAWriteIsMadeOf));
    }

    /// <summary>
    /// Every name in the set is one that exists. A vocabulary that has drifted
    /// from the tree or from the server cannot fire, and it reads exactly like
    /// one that is passing.
    /// </summary>
    [Fact]
    public void EveryNameInTheSetIsOneThatExists()
    {
        Assert.Empty(WhatAWriteIsMadeOf.Where(name => Resolve(name) is null).ToList());
    }

    /// <summary>
    /// The plan an operator reads is the plan the apply carries out, asserted
    /// against what the write path was handed rather than against a second
    /// expectation. A route that reached no applier and planned something else
    /// would pass every leg above.
    /// </summary>
    /// <remarks>
    /// It runs over a pairing an earlier pass was part of the way through,
    /// because that is where the two derivations can differ at all. Over a
    /// library nothing has been through, a pass that asked this route and a pass
    /// that derived its own plan from the whole request produce the same answer,
    /// and a second derivation would be invisible here.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ADryRunNamesExactlyWhatAPassThenWrites()
    {
        var items = Items(3);
        var progress = new RecordingPassProgress();
        progress.Completed(_pairing, items[0]);

        var intended = DryRun.Of(RequestFor(items), progress);

        var target = new TargetThatStops(Guid.Empty);
        await new Pass(new Applier(target, new RecordingWrittenValues()), progress)
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.Equal(
            intended.Plan.Items.Select(item => item.LocalItemId).Order().ToList(),
            target.Written.Select(item => item.LocalItemId).Order().ToList());

        Assert.Equal(
            intended.Plan.FieldsToWrite,
            target.Written.Sum(item => item.Changes.Count(change => change.Writes)));
    }

    /// <summary>
    /// And it says so of a library there is something to say about, so the
    /// comparison above is not two empty sets agreeing.
    /// </summary>
    [Fact]
    public void ADryRunOverALibraryWithSomethingToChangeSaysSo()
    {
        var intended = DryRun.Of(RequestFor(Items(3)), new RecordingPassProgress());

        Assert.Equal(3, intended.Plan.Items.Count);
        Assert.Equal(3, intended.Plan.FieldsToWrite);
    }

    /// <summary>
    /// A dry run taken after an interrupted pass leaves out what that pass
    /// finished with, and counts it. The skip is derived once and in one place,
    /// so an operator reading a plan after an interruption reads what the resume
    /// would do rather than what a first pass would have done.
    /// </summary>
    [Fact]
    public void ADryRunAfterAnInterruptionLeavesOutWhatIsAlreadyDone()
    {
        var items = Items(4);
        var progress = new RecordingPassProgress();
        progress.Completed(_pairing, items[0]);
        progress.Completed(_pairing, items[2]);

        var intended = DryRun.Of(RequestFor(items), progress);

        Assert.Equal(
            new[] { items[1], items[3] }.Order().ToList(),
            intended.Plan.Items.Select(item => item.LocalItemId).Order().ToList());

        Assert.Equal(2, intended.ItemsAlreadyDone);
    }

    /// <summary>
    /// Nothing to plan from is refused as the caller calls.
    /// </summary>
    [Fact]
    public void ADryRunFromARequestThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => DryRun.Of(null!, new RecordingPassProgress()));
    }

    /// <summary>
    /// And so is nowhere to read how far an earlier pass got, because a dry run
    /// taken without one describes a pass that is not the one an apply would
    /// make.
    /// </summary>
    [Fact]
    public void ADryRunWithNowhereToReadProgressIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => DryRun.Of(new PlanRequest(), null!));
    }

    /// <summary>
    /// The neighbour to both refusals. Everything is there, and a request with
    /// nothing in it is an empty plan rather than a refusal.
    /// </summary>
    [Fact]
    public void ADryRunOverAnEmptyRequestIsAnEmptyPlan()
    {
        var intended = DryRun.Of(new PlanRequest(), new RecordingPassProgress());

        Assert.Empty(intended.Plan.Items);
        Assert.Equal(0, intended.ItemsAlreadyDone);
    }

    private static bool OnThePlanOnlyPath(string name)
    {
        return string.Equals(name, typeof(DryRun).FullName, StringComparison.Ordinal)
            || string.Equals(name, typeof(DryRunResult).FullName, StringComparison.Ordinal);
    }

    private static Type? Resolve(string full)
    {
        return typeof(Plugin).Assembly.GetType(full)
            ?? Type.GetType(full)
            ?? typeof(BaseItem).Assembly.GetType(full)
            ?? typeof(MediaBrowser.Model.Entities.MetadataField).Assembly.GetType(full);
    }

    private static List<Guid> Items(int count) =>
        Enumerable.Range(1, count)
            .Select(n => new Guid(string.Format(CultureInfo.InvariantCulture, "bbbbbbbb-0000-0000-0000-{0:D12}", n)))
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
