using System;
using System.Collections;
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
/// <para>
/// There are three sweeps because there are three types. The row was swept and
/// the two above it were named by hand, so a property added to the plan or to
/// an item was covered by whichever assertion somebody remembered to extend.
/// <c>LastSavedWhenPlanned</c> is what that cost: the token the write path
/// compares to decide whether anything else wrote the item since, carried on
/// every item plan, and asserted by nothing here. A plan that lost it on the
/// way through the serialised form is refused item by item at apply time, for a
/// reason that names the item rather than the export.
/// </para>
/// <para>
/// A sweep proves nothing about a property the fixture leaves at its default,
/// because a dropped default reads back as itself.
/// <c>EveryPropertySweptCarriesAValueTheFixtureCouldLose</c> is what stops the
/// two sweeps below passing that way, and the one thing it excuses it excuses
/// by deriving rather than by name.
/// </para>
/// </remarks>
public class PlanRoundTripTests
{
    /// <summary>
    /// Gets the properties of a plan row that carry a decision, so the sweep is
    /// derived from the type rather than from a list beside it.
    /// </summary>
    public static TheoryData<string> ChangeProperties => NamesOn(typeof(PlannedChange));

    /// <summary>
    /// Gets the properties of the plan itself, derived the same way.
    /// </summary>
    public static TheoryData<string> PlanProperties => NamesOn(typeof(Plan));

    /// <summary>
    /// Gets the properties of what the plan says about one item, derived the
    /// same way.
    /// </summary>
    public static TheoryData<string> ItemProperties => NamesOn(typeof(ItemPlan));

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
    /// Every property of the plan itself survives, swept off the type for the
    /// same reason the rows are.
    /// </summary>
    /// <param name="property">The property, from the plan type.</param>
    [Theory]
    [MemberData(nameof(PlanProperties))]
    public void EveryPropertyOfThePlanSurvivesBeingWrittenDownAndReadBack(string property)
    {
        var before = APlanWithOneOfEachDisposition();
        var after = RoundTrip(before);

        var reader = typeof(Plan).GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(reader);

        Assert.Equal(Written(reader.GetValue(before)), Written(reader.GetValue(after)));
    }

    /// <summary>
    /// Every property of an item plan survives. This is the sweep that covers
    /// the token the write path compares, which no assertion here reached while
    /// the two types above the row were named by hand.
    /// </summary>
    /// <param name="property">The property, from the item plan type.</param>
    [Theory]
    [MemberData(nameof(ItemProperties))]
    public void EveryPropertyOfAnItemSurvivesBeingWrittenDownAndReadBack(string property)
    {
        var before = APlanWithOneOfEachDisposition();
        var after = RoundTrip(before);

        var reader = typeof(ItemPlan).GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(reader);

        Assert.Equal(before.Items.Count, after.Items.Count);
        Assert.Equal(Written(reader.GetValue(before.Items[0])), Written(reader.GetValue(after.Items[0])));
    }

    /// <summary>
    /// The fixture carries something to lose on every property the two sweeps
    /// above read. Without this they pass on a property that is dropped, since
    /// a value left at its default reads back as its default.
    /// </summary>
    /// <remarks>
    /// One shape is excused and it is derived rather than named. A property
    /// whose type declares a single member cannot be given a value that would
    /// tell a carried one from a dropped one, and <c>SyncDirection</c> declares
    /// one member on purpose. The excuse expires on its own the day a second is
    /// added, which is the day the property starts being able to lose
    /// something.
    /// </remarks>
    [Fact]
    public void EveryPropertySweptCarriesAValueTheFixtureCouldLose()
    {
        var plan = APlanWithOneOfEachDisposition();

        var empty = Sweep(typeof(Plan))
            .Where(property => !CannotHoldATellingValue(property))
            .Where(property => !CarriesSomethingToLose(property, property.GetValue(plan)))
            .Select(property => "Plan." + property.Name)
            .Concat(Sweep(typeof(ItemPlan))
                .Where(property => !CannotHoldATellingValue(property))
                .Where(property => !CarriesSomethingToLose(property, property.GetValue(plan.Items[0])))
                .Select(property => "ItemPlan." + property.Name))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            empty.Count == 0,
            "The fixture leaves a swept property at its default, so the sweep over it would pass even if the "
            + "property were dropped on the way through: " + string.Join("; ", empty));
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

    /// <summary>
    /// The properties of a type that a sweep reads, in one place so the three
    /// sweeps cannot disagree about what a property is.
    /// </summary>
    /// <param name="type">The type being swept.</param>
    /// <returns>Its public instance properties.</returns>
    private static PropertyInfo[] Sweep(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
    }

    private static TheoryData<string> NamesOn(Type type)
    {
        var names = new TheoryData<string>();
        foreach (var property in Sweep(type))
        {
            names.Add(property.Name);
        }

        return names;
    }

    /// <summary>
    /// A property's value as text, so a collection is compared by what it holds
    /// rather than by which object holds it. Comparing the values directly
    /// would pass on any two distinct collections.
    /// </summary>
    /// <param name="value">The value read off the property.</param>
    /// <returns>The value written down.</returns>
    private static string Written(object? value)
    {
        return JsonSerializer.Serialize(value);
    }

    /// <summary>
    /// Whether a property could tell a carried value from a dropped one at all.
    /// </summary>
    /// <param name="property">The property being swept.</param>
    /// <returns>True where no value it can hold would differ from its default.</returns>
    private static bool CannotHoldATellingValue(PropertyInfo property)
    {
        return property.PropertyType.IsEnum && Enum.GetValues(property.PropertyType).Length < 2;
    }

    /// <summary>
    /// Whether the fixture put something on this property that being dropped
    /// would destroy.
    /// </summary>
    /// <remarks>
    /// Everything that is not a collection is answered by comparing against the
    /// property type's own default, rather than by listing the shapes a default
    /// is spelled in. An earlier version of this listed them, and an enum
    /// sitting on its zero member fell through the list and was answered yes,
    /// which is the case the excuse beside this one exists for.
    /// </remarks>
    /// <param name="property">The property being swept.</param>
    /// <param name="value">The value the fixture carries.</param>
    /// <returns>True where the value is not the default for its type.</returns>
    private static bool CarriesSomethingToLose(PropertyInfo property, object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string text)
        {
            return text.Length > 0;
        }

        // A collection's default is no collection at all, and an empty one read
        // back as an empty one loses nothing, so both answer no here.
        if (value is IEnumerable held)
        {
            return held.Cast<object?>().Any();
        }

        var unset = property.PropertyType.IsValueType
            ? Activator.CreateInstance(property.PropertyType)
            : null;

        return !value.Equals(unset);
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

            // In the spelling `LibraryPlanTarget.StampOf` produces, written out
            // rather than read off a clock, because the value only has to be
            // one the round trip could lose.
            LastSavedHere = "2026-02-03T04:05:06.7890123Z",
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
