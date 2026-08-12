using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// A plan survives being written down and read back.
/// </summary>
/// <remarks>
/// The property matters because a plan is shown to an operator, recorded, and
/// then acted on, and those need not happen inside one run. A plan that lost a
/// field on the way through would be a plan an operator approved and a
/// different plan the applier carried out, and nothing about that failure looks
/// wrong at either end.
/// <para>
/// The comparison is property by property rather than by comparing two objects,
/// because a plan is a class and a reference comparison would pass on anything.
/// A property added to a plan row with no place in the serialised form is
/// refused by the sweep below rather than discovered when it turns out to be
/// null on the other side.
/// </para>
/// </remarks>
public class PlanRoundTripTests
{
    /// <summary>
    /// Gets the properties of a plan row that carry a decision, so the sweep is
    /// derived from the type rather than from a list beside it.
    /// </summary>
    public static TheoryData<string> ChangeProperties
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var property in typeof(PlannedChange).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                names.Add(property.Name);
            }

            return names;
        }
    }

    /// <summary>
    /// Every property of every row survives the round trip. The sweep is over
    /// the type, so a property added later is carried into this test without
    /// anybody remembering to add it.
    /// </summary>
    /// <param name="property">The property, from the row type.</param>
    [Theory]
    [MemberData(nameof(ChangeProperties))]
    public void EveryPropertyOfARowSurvivesBeingWrittenDownAndReadBack(string property)
    {
        var read = RoundTrip(APlanWithOneOfEachDisposition());

        var before = APlanWithOneOfEachDisposition().Items[0].Changes;
        var after = read.Items[0].Changes;

        Assert.Equal(before.Count, after.Count);

        var reader = typeof(PlannedChange).GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(reader);

        for (var i = 0; i < before.Count; i++)
        {
            Assert.Equal(reader.GetValue(before[i]), reader.GetValue(after[i]));
        }
    }

    /// <summary>
    /// The plan's own properties survive too, and so does the item it is about.
    /// </summary>
    [Fact]
    public void ThePlanAndItsItemsSurviveBeingWrittenDownAndReadBack()
    {
        var before = APlanWithOneOfEachDisposition();
        var after = RoundTrip(before);

        Assert.Equal(before.PairingId, after.PairingId);
        Assert.Equal(before.Direction, after.Direction);
        Assert.Equal(before.Items.Count, after.Items.Count);
        Assert.Equal(before.Items[0].LocalItemId, after.Items[0].LocalItemId);
        Assert.Equal(before.Items[0].PeerItemId, after.Items[0].PeerItemId);
        Assert.Equal(before.Items[0].Kind, after.Items[0].Kind);
    }

    /// <summary>
    /// What the applier obeys survives as well, and it is derived on the far
    /// side from the rows rather than read out of the text. A plan read back
    /// with the rows intact and the flag lost would be a plan that writes
    /// nothing, which is the quiet direction of that failure.
    /// </summary>
    [Fact]
    public void WhatThePlanWritesIsTheSameAfterTheRoundTrip()
    {
        var before = APlanWithOneOfEachDisposition();
        var after = RoundTrip(before);

        Assert.True(before.FieldsToWrite > 0, "The fixture writes nothing, so this proves nothing.");
        Assert.Equal(before.FieldsToWrite, after.FieldsToWrite);
        Assert.Equal(before.FieldsConsidered, after.FieldsConsidered);
        Assert.Equal(before.Items[0].Writes, after.Items[0].Writes);
    }

    /// <summary>
    /// The fixture is a real plan rather than one written by hand, so what is
    /// round-tripped is what the planner produces. A hand-built plan can be in
    /// a shape the planner never makes, and a round trip over one proves the
    /// serialiser and not the type.
    /// </summary>
    [Fact]
    public void TheFixtureIsAPlanThePlannerProduced()
    {
        var plan = APlanWithOneOfEachDisposition();

        var dispositions = plan.Items[0].Changes.Select(change => change.Disposition).ToHashSet();

        Assert.Equal(Enum.GetValues<PlanDisposition>().ToHashSet(), dispositions);
    }

    private static Plan RoundTrip(Plan plan)
    {
        var text = JsonSerializer.Serialize(plan);
        var read = JsonSerializer.Deserialize<Plan>(text);
        Assert.NotNull(read);
        return read;
    }

    /// <summary>
    /// A plan carrying one row of every disposition, so the round trip is over
    /// every shape a row can have rather than over the easy one.
    /// </summary>
    private static Plan APlanWithOneOfEachDisposition()
    {
        var item = new ItemObservation
        {
            LocalItemId = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
            PeerItemId = new Guid("bbbbbbbb-0000-0000-0000-000000000002"),
            Kind = "Movie",
        };

        // Overview moves and the peer has the only value, so it is written.
        item.Fields.Add(Observed("Overview", local: null, peer: "the peer's overview"));

        // Genres is declared and does not move.
        item.Fields.Add(Observed("Genres", local: "ours", peer: "theirs"));

        // Tags moves and is excluded below.
        item.Fields.Add(Observed("Tags", local: "ours", peer: "theirs"));

        // EndDate is declared for the series tree and this is a film.
        item.Fields.Add(Observed("EndDate", local: "ours", peer: "theirs"));

        // No row at all.
        item.Fields.Add(Observed("NoSuchField", local: "ours", peer: "theirs"));

        var request = new PlanRequest
        {
            PairingId = new Guid("11111111-2222-3333-4444-555555555555"),
            Direction = SyncDirection.TwoWay,
        };

        request.ExcludedFields.Add("Tags");
        request.Items.Add(item);

        var plan = Planner.Plan(request);

        // The decided rows above are one write and one refusal, which is what
        // makes the flag worth round-tripping at all.
        Assert.Contains(plan.Items[0].Changes, change => change.Outcome == ConflictOutcome.TakePeer);

        return plan;
    }

    private static FieldObservation Observed(string field, string? local, string? peer)
    {
        return new FieldObservation
        {
            Field = field,
            LocalValue = local,
            PeerValue = peer,
            LastWrittenByThisPlugin = null,
            FieldLockedHere = false,
            FieldLockedOnPeer = false,
        };
    }
}
