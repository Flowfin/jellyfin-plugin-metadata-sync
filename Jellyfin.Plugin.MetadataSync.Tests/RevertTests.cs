using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Taking back what arrived through a sync, against a synthetic library and the
/// record of what this plugin wrote.
/// </summary>
/// <remarks>
/// Every case here is arranged by writing down what the library holds and what
/// the record says, so nothing runs and nothing is substituted for a server. The
/// decision under test is #1's, taken on 2026-08-09, and the rule that bounds it
/// is #66's: a value is put back only where this plugin can prove it wrote what
/// is there now AND can produce what was there before it ever wrote.
/// <para>
/// The two proofs fail in different ways and each has its own count, because an
/// operator confirming a revert is owed the difference between a field that was
/// never this plugin's and a field that was and has been edited since. The
/// second proof is the one that is easy to assume: a history standing at the
/// bound may already have lost the write that came first, the discard is not
/// recorded, and restoring the earliest value still held would put the peer's
/// own value back in the name of removing it.
/// </para>
/// </remarks>
public class RevertTests
{
    private const string Field = "Overview";
    private const string Tagline = "Tagline";

    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid _item = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid _second = new("aaaaaaaa-0000-0000-0000-000000000002");

    /// <summary>
    /// The ordinary case. This plugin wrote a field, nobody has touched it
    /// since, and the value that was there before the first write goes back.
    /// </summary>
    [Fact]
    public void AFieldThisPluginWroteGoesBackToWhatWasThereBefore()
    {
        var written = new RecordingWrittenValues();
        written.Record(_pairing, _item, Field, "what the peer said", "what this library had");

        var plan = Revert.Plan(RequestFor(Held(Field, "what the peer said")), written);

        var change = Assert.Single(Assert.Single(plan.Plan.Items).Changes);

        Assert.True(change.Writes);
        Assert.Equal("what this library had", change.ValueToWrite);
        Assert.Equal(1, plan.FieldsToRestore);
        Assert.Equal(1, plan.ItemsToWrite);
    }

    /// <summary>
    /// A field that held nothing before this plugin wrote goes back to holding
    /// nothing. An absence is a value that was there, and a revert that left the
    /// peer's value in place because the earlier one was empty would keep
    /// exactly the fields the sync created.
    /// </summary>
    [Fact]
    public void AFieldThatHeldNothingBeforeGoesBackToHoldingNothing()
    {
        var written = new RecordingWrittenValues();
        written.Record(_pairing, _item, Field, "what the peer said", null);

        var change = Assert.Single(Assert.Single(
            Revert.Plan(RequestFor(Held(Field, "what the peer said")), written).Plan.Items).Changes);

        Assert.True(change.Writes);
        Assert.Null(change.ValueToWrite);
    }

    /// <summary>
    /// A field this plugin has no record of writing is left alone and counted.
    /// It is #66's rule and the reason a revert is allowable at all: a missing
    /// record is evidence that the field is not this plugin's, never an
    /// invitation to assume.
    /// </summary>
    [Fact]
    public void AFieldWithNoRecordIsLeftAloneAndCounted()
    {
        var plan = Revert.Plan(
            RequestFor(Held(Field, "what somebody here typed")),
            new RecordingWrittenValues());

        Assert.Empty(plan.Plan.Items);
        Assert.Equal(1, plan.FieldsWithNoRecord);
        Assert.Equal(0, plan.FieldsToRestore);
    }

    /// <summary>
    /// A library where most fields are unattributed has none of them touched.
    /// This is the case #66's third condition asks for, and it is a different
    /// question from the one above: a revert with something real to do must not
    /// take the fields beside it with it.
    /// </summary>
    [Fact]
    public void ALibraryOfMostlyUnattributedFieldsIsLeftAlone()
    {
        var written = new RecordingWrittenValues();
        written.Record(_pairing, _item, Field, "what the peer said", "what this library had");

        var request = new RevertRequest { PairingId = _pairing };

        // One item this plugin wrote one field on, and nine it never touched.
        request.Items.Add(ItemOf(_item, Held(Field, "what the peer said"), Held(Tagline, "somebody's tagline")));

        for (var n = 2; n <= 10; n++)
        {
            request.Items.Add(ItemOf(
                Item(n),
                Held(Field, "somebody's overview"),
                Held(Tagline, "somebody's tagline")));
        }

        var plan = Revert.Plan(request, written);

        var item = Assert.Single(plan.Plan.Items);

        Assert.Equal(_item, item.LocalItemId);
        Assert.Equal(new[] { Field }, item.Changes.Select(change => change.Field).ToArray());
        Assert.Equal(19, plan.FieldsWithNoRecord);
        Assert.Equal(1, plan.FieldsToRestore);
    }

    /// <summary>
    /// A field this plugin wrote and somebody here has changed since is left
    /// alone and counted separately. The record exists and says this plugin
    /// wrote something else, so putting the earlier value back would delete an
    /// edit made on this server.
    /// </summary>
    [Fact]
    public void AFieldEditedHereSinceIsLeftAloneAndCountedOnItsOwn()
    {
        var written = new RecordingWrittenValues();
        written.Record(_pairing, _item, Field, "what the peer said", "what this library had");

        var plan = Revert.Plan(RequestFor(Held(Field, "what somebody here typed afterwards")), written);

        Assert.Empty(plan.Plan.Items);
        Assert.Equal(1, plan.FieldsChangedSinceThisPluginWroteThem);
        Assert.Equal(0, plan.FieldsWithNoRecord);
    }

    /// <summary>
    /// The not-known case, with the outcome it is given: nothing is written and
    /// it is counted on its own. A history standing at the bound is one the
    /// bound may already have taken the first write out of, and the discard is
    /// not recorded, so the earliest value still held cannot be shown to predate
    /// this pairing.
    /// </summary>
    [Fact]
    public void AFieldWhoseEarlierValueIsNotKnownIsLeftAloneAndCounted()
    {
        var written = new RecordingWrittenValues();

        for (var n = 1; n <= WrittenValues.Bound; n++)
        {
            written.Record(_pairing, _item, Field, Value(n), Value(n - 1));
        }

        var plan = Revert.Plan(RequestFor(Held(Field, Value(WrittenValues.Bound))), written);

        Assert.Empty(plan.Plan.Items);
        Assert.Equal(1, plan.FieldsWhoseEarlierValueIsNotKnown);
        Assert.Equal(0, plan.FieldsChangedSinceThisPluginWroteThem);
        Assert.Equal(0, plan.FieldsWithNoRecord);
    }

    /// <summary>
    /// The neighbour for the case above, one write short of the bound. Nothing
    /// has been discarded below the bound, because a value is dropped only once
    /// the bound is exceeded, so the earliest value still held is provably the
    /// one that was there before this plugin first wrote.
    /// </summary>
    [Fact]
    public void OneWriteShortOfTheBoundTheEarlierValueIsStillKnown()
    {
        var written = new RecordingWrittenValues();

        for (var n = 1; n <= WrittenValues.Bound - 1; n++)
        {
            written.Record(_pairing, _item, Field, Value(n), Value(n - 1));
        }

        var plan = Revert.Plan(RequestFor(Held(Field, Value(WrittenValues.Bound - 1))), written);

        Assert.Equal(Value(0), Assert.Single(Assert.Single(plan.Plan.Items).Changes).ValueToWrite);
        Assert.Equal(0, plan.FieldsWhoseEarlierValueIsNotKnown);
    }

    /// <summary>
    /// A field this plugin wrote on an item nobody read is neither restored nor
    /// forgotten. A value cannot be put back on an item that was not read, and a
    /// count above zero says the revert covered less than what this pairing
    /// touched.
    /// </summary>
    [Fact]
    public void AFieldOnAnItemNobodyReadIsCountedRatherThanSilent()
    {
        var written = new RecordingWrittenValues();
        written.Record(_pairing, _item, Field, "what the peer said", "what this library had");
        written.Record(_pairing, _second, Field, "what the peer said", "what this library had");

        var plan = Revert.Plan(RequestFor(Held(Field, "what the peer said")), written);

        Assert.Equal(1, plan.FieldsToRestore);
        Assert.Equal(1, plan.FieldsOnItemsNotRead);
    }

    /// <summary>
    /// Another pairing's rows are not this revert's. A revocation ends one
    /// pairing, and a revert that reached across would take back what a
    /// relationship that is still live had put there.
    /// </summary>
    [Fact]
    public void AnotherPairingsWritesAreNotTakenBack()
    {
        var written = new RecordingWrittenValues();
        written.Record(
            new Guid("cccccccc-0000-0000-0000-000000000009"),
            _item,
            Field,
            "what the other peer said",
            "what this library had");

        var plan = Revert.Plan(RequestFor(Held(Field, "what the other peer said")), written);

        Assert.Empty(plan.Plan.Items);
        Assert.Equal(1, plan.FieldsWithNoRecord);
        Assert.Equal(0, plan.FieldsOnItemsNotRead);
    }

    /// <summary>
    /// The counts are answered before anything happens, which is what makes them
    /// a confirmation rather than a report. Deciding a revert writes nothing by
    /// itself, and the sentence saying what the counts do not cover travels with
    /// them.
    /// </summary>
    [Fact]
    public void TheCountsAreAnsweredBeforeAnythingIsWritten()
    {
        var written = new RecordingWrittenValues();
        written.Record(_pairing, _item, Field, "what the peer said", "what this library had");

        var target = new RecordingPlanTarget();
        var plan = Revert.Plan(RequestFor(Held(Field, "what the peer said")), written);

        Assert.Empty(target.Written);
        Assert.Equal(1, plan.FieldsToRestore);
        Assert.Contains("nobody read", RevertPlan.WhatTheseCountsDoNotSay, StringComparison.Ordinal);
    }

    /// <summary>
    /// An interrupted revert is run again, and the second run leaves the library
    /// where the first one left it. That is what idempotent has to mean here:
    /// not that the second plan is the same object, but that carrying it out
    /// moves nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ARevertRunAgainMovesNothing()
    {
        var written = new RecordingWrittenValues();
        written.Record(_pairing, _item, Field, "what the peer said", "what this library had");

        var library = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [Field] = "what the peer said",
        };

        for (var run = 0; run < 2; run++)
        {
            var target = new RecordingPlanTarget();
            var plan = Revert.Plan(RequestFor(Held(Field, library[Field])), written);

            await new Applier(target, written).ApplyAsync(plan.Plan, CancellationToken.None);

            foreach (var change in target.Written.SelectMany(item => item.Changes).Where(change => change.Writes))
            {
                library[change.Field] = change.ValueToWrite;
            }
        }

        Assert.Equal("what this library had", library[Field]);
    }

    /// <summary>
    /// The plan a revert produces is carried out by the ordinary write path, so
    /// an item whose fields cannot all be written is left holding what it held
    /// and no second implementation of that rule exists.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ARevertIsCarriedOutByTheOrdinaryWritePath()
    {
        var written = new RecordingWrittenValues();
        written.Record(_pairing, _item, Field, "what the peer said", "what this library had");

        var target = new RecordingPlanTarget();
        var plan = Revert.Plan(RequestFor(Held(Field, "what the peer said")), written);

        var result = await new Applier(target, written).ApplyAsync(plan.Plan, CancellationToken.None);

        Assert.Equal(1, result.ItemsWritten);
        Assert.Equal(1, result.FieldsWritten);
        Assert.Equal(_item, Assert.Single(target.Written).LocalItemId);
    }

    /// <summary>
    /// The stamp the write path compares travels onto the plan. Without it the
    /// write is refused rather than made blind, so a revert that dropped it
    /// would be a revert that never writes.
    /// </summary>
    [Fact]
    public void TheStampTheWritePathComparesTravelsOntoThePlan()
    {
        var written = new RecordingWrittenValues();
        written.Record(_pairing, _item, Field, "what the peer said", "what this library had");

        var plan = Revert.Plan(RequestFor(Held(Field, "what the peer said")), written);

        Assert.Equal("when this server last saved it", Assert.Single(plan.Plan.Items).LastSavedWhenPlanned);
    }

    /// <summary>
    /// The set of fields one pairing touched is answered from the record rather
    /// than by reading a library, which is #64's first condition. It is asked of
    /// a store holding two pairings and answers only about the one asked for.
    /// </summary>
    [Fact]
    public void TheFieldsOnePairingTouchedAreAnsweredWithoutReadingALibrary()
    {
        var written = new RecordingWrittenValues();
        written.Record(_pairing, _second, Tagline, "theirs", "ours");
        written.Record(_pairing, _item, Field, "theirs", "ours");
        written.Record(new Guid("cccccccc-0000-0000-0000-000000000009"), _item, Field, "theirs", "ours");

        Assert.Equal(
            new[] { (_item, Field), (_second, Tagline) },
            written.Fields(_pairing).Select(key => (key.Item, key.Field)).ToArray());
    }

    /// <summary>
    /// A revert with no request to decide from is refused.
    /// </summary>
    [Fact]
    public void ARevertWithNoRequestIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => Revert.Plan(null!, new RecordingWrittenValues()));
    }

    /// <summary>
    /// A revert with no record to decide against is refused. A default would be
    /// a revert that proves nothing about what it is putting back, which is the
    /// one thing #66 forbids.
    /// </summary>
    [Fact]
    public void ARevertWithNoRecordIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => Revert.Plan(new RevertRequest(), null!));
    }

    private static string Value(int n) => string.Format(CultureInfo.InvariantCulture, "value {0}", n);

    private static Guid Item(int n) =>
        new(string.Format(CultureInfo.InvariantCulture, "aaaaaaaa-0000-0000-0000-{0:D12}", n));

    private static RevertField Held(string field, string? value) =>
        new() { Field = field, LocalValue = value };

    private static RevertObservation ItemOf(Guid id, params RevertField[] fields)
    {
        var observation = new RevertObservation
        {
            LocalItemId = id,
            Kind = "Movie",
            LastSavedHere = "when this server last saved it",
        };

        foreach (var field in fields)
        {
            observation.Fields.Add(field);
        }

        return observation;
    }

    private static RevertRequest RequestFor(params RevertField[] fields)
    {
        var request = new RevertRequest { PairingId = _pairing };
        request.Items.Add(ItemOf(_item, fields));
        return request;
    }
}
