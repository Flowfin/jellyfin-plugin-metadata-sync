using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The store of what this plugin wrote, and the three states a field can be in
/// because of it.
/// </summary>
/// <remarks>
/// The point of the record is not that it persists. It is that a later pass can
/// tell a value this plugin put there from a value somebody on this server
/// typed, without asking either server's clock. So the cases below run the two
/// halves that matter through the planner rather than asserting the store's
/// answers on their own: a value equal to the recorded one is an update, and a
/// value differing from it is a local edit, and those are two different rules
/// firing rather than two readings of one field.
/// <para>
/// Each case gets a directory of its own and removes it afterwards. A store is a
/// file, and a suite that shared one would have cases that pass in the order
/// somebody ran them.
/// </para>
/// </remarks>
public class WrittenValuesTests
{
    private const string Field = "Overview";
    private const string AKindEveryGroupHolds = "Movie";

    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid _item = new("aaaaaaaa-0000-0000-0000-000000000001");

    /// <summary>
    /// A field this plugin has never written has no record, and the empty list
    /// is what says so. It is the first of the three cases the issue names, and
    /// it is the state every field on every item starts in.
    /// </summary>
    [Fact]
    public void AFieldThatWasNeverWrittenHasNoRecord()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        Assert.Null(store.LastWritten(_pairing, _item, Field));
        Assert.Empty(store.History(_pairing, _item, Field));
    }

    /// <summary>
    /// The second of the three cases. This plugin wrote a value, nobody here has
    /// touched it since, and the peer has moved on: the values differ and the
    /// difference is this plugin's own work catching up rather than two
    /// operators disagreeing.
    /// </summary>
    [Fact]
    public void AValueThisPluginWroteAndNobodyEditedIsUpdatedRatherThanTreatedAsAConflict()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        store.Record(_pairing, _item, Field, "what the peer said last time", null);

        var change = PlanOneField(
            local: "what the peer said last time",
            peer: "what the peer says now",
            store);

        Assert.Equal(ConflictOutcome.TakePeer, change.Outcome);
        Assert.Equal("local-unchanged-since-this-plugin-wrote-it", change.Rule);
        Assert.True(change.Writes);
    }

    /// <summary>
    /// The third of the three cases, and the one the whole record exists for. The
    /// operator edited the overview after this plugin wrote it, so the value here
    /// is not the one this plugin left, and no rule may overwrite it as stale.
    /// </summary>
    [Fact]
    public void AValueEditedHereAfterThisPluginWroteItIsNotOverwritten()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        store.Record(_pairing, _item, Field, "what the peer said last time", null);

        var change = PlanOneField(
            local: "what the household actually wants to read",
            peer: "what the peer says now",
            store);

        Assert.False(change.Writes);
        Assert.Equal(ConflictOutcome.Refuse, change.Outcome);
        Assert.Null(change.Rule);
    }

    /// <summary>
    /// The same arrangement as the case above with the record absent, which is
    /// the neighbour that separates the record from everything else in the room.
    /// Without a record the two differing values are a plain disagreement and no
    /// rule decides them either, so the outcome alone proves nothing: what
    /// changes with the record present is which rule fired, and that is what the
    /// two cases above assert.
    /// </summary>
    [Fact]
    public void WithNoRecordAtAllTheSameTwoValuesAreDecidedByNoRule()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        var change = PlanOneField(
            local: "what the peer said last time",
            peer: "what the peer says now",
            store);

        Assert.False(change.Writes);
        Assert.Null(change.Rule);
    }

    /// <summary>
    /// The bound, decided on #16 on 2026-08-24: ten values per item and per
    /// field, oldest dropped first. Both halves are asserted, because a store
    /// that kept the ten oldest would also hold ten.
    /// </summary>
    [Fact]
    public void TheBoundKeepsTheNewestTenAndDropsTheOldestFirst()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        for (var n = 1; n <= WrittenValues.Bound + 3; n++)
        {
            store.Record(_pairing, _item, Field, n.ToString(CultureInfo.InvariantCulture), (n - 1).ToString(CultureInfo.InvariantCulture));
        }

        var held = store.History(_pairing, _item, Field);

        Assert.Equal(WrittenValues.Bound, held.Count);
        Assert.Equal("4", held[0].Value);
        Assert.Equal("13", held[^1].Value);
        Assert.Equal("13", store.LastWritten(_pairing, _item, Field));
    }

    /// <summary>
    /// The record outlives the instance that wrote it. A plugin restart and a
    /// server restart both end the object and leave the directory, and this is
    /// that from the store's side: a second instance over the same directory
    /// answers what the first one was told.
    /// </summary>
    /// <remarks>
    /// What this does not do is stop and start a server. No test here does, the
    /// headless register records that refusal by name, and this case is the
    /// property that survives without one rather than a substitute pretending to
    /// be the same thing.
    /// </remarks>
    [Fact]
    public void ARecordSurvivesTheInstanceThatWroteIt()
    {
        using var directory = new TemporaryDirectory();

        new WrittenValues(directory.Path).Record(_pairing, _item, Field, "written before the restart", "what it replaced");

        var afterRestart = new WrittenValues(directory.Path);

        Assert.Equal("written before the restart", afterRestart.LastWritten(_pairing, _item, Field));
    }

    /// <summary>
    /// The bound survives the restart too, and it survives it in the same
    /// direction. A store that applied the bound only on the way in would read
    /// a longer file back and answer with more than it may hold.
    /// </summary>
    [Fact]
    public void TheBoundIsAppliedToWhatIsReadBackAsWellAsToWhatIsWritten()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        for (var n = 1; n <= WrittenValues.Bound + 3; n++)
        {
            store.Record(_pairing, _item, Field, n.ToString(CultureInfo.InvariantCulture), (n - 1).ToString(CultureInfo.InvariantCulture));
        }

        var afterRestart = new WrittenValues(directory.Path);
        var held = afterRestart.History(_pairing, _item, Field);

        Assert.Equal(WrittenValues.Bound, held.Count);
        Assert.Equal("4", held[0].Value);
        Assert.Equal("13", held[^1].Value);
        Assert.Equal("3", held[0].Previous);
        Assert.Equal("12", held[^1].Previous);
    }

    /// <summary>
    /// Two keys do not run into each other. A store keyed by the item alone, or
    /// by the field alone, would pass every case above and answer one item's
    /// question with another item's value.
    /// </summary>
    [Fact]
    public void EachPairingItemAndFieldIsItsOwnRecord()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);
        var otherPairing = new Guid("dddddddd-0000-0000-0000-000000000004");
        var otherItem = new Guid("eeeeeeee-0000-0000-0000-000000000005");

        store.Record(_pairing, _item, Field, "this one", null);

        Assert.Null(store.LastWritten(otherPairing, _item, Field));
        Assert.Null(store.LastWritten(_pairing, otherItem, Field));
        Assert.Null(store.LastWritten(_pairing, _item, "Tagline"));
        Assert.Equal("this one", store.LastWritten(_pairing, _item, Field));
    }

    /// <summary>
    /// A pass killed part way through a write leaves a line that never finished.
    /// The store opens anyway, keeps every line that did finish, and counts the
    /// one that did not, because a store that refused to open after a power cut
    /// would turn one lost write into every lost write.
    /// </summary>
    [Fact]
    public void ALineThatNeverFinishedIsDroppedAndCountedRatherThanThrown()
    {
        using var directory = new TemporaryDirectory();

        new WrittenValues(directory.Path).Record(_pairing, _item, Field, "the write that finished", null);

        var file = Path.Combine(directory.Path, WrittenValues.FileName);
        File.AppendAllText(file, "{\"Pairing\":\"cccccccc-0000-0000", new UTF8Encoding(false));

        var afterTheKill = new WrittenValues(directory.Path);

        Assert.Equal(1, afterTheKill.Unreadable);
        Assert.Equal("the write that finished", afterTheKill.LastWritten(_pairing, _item, Field));
    }

    /// <summary>
    /// A store that appended for ever would grow without end on a library that
    /// syncs the same fields every pass. It rewrites itself instead, and the
    /// rewrite keeps every answer: the file gets shorter and nothing it said
    /// changes.
    /// </summary>
    [Fact]
    public void AFileCarryingMoreSupersededLinesThanItNeedsIsRewritten()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);
        var file = Path.Combine(directory.Path, WrittenValues.FileName);

        for (var n = 1; n <= 2000; n++)
        {
            store.Record(_pairing, _item, Field, n.ToString(CultureInfo.InvariantCulture), (n - 1).ToString(CultureInfo.InvariantCulture));
        }

        var lines = File.ReadAllLines(file).Length;

        Assert.True(lines < 2000, "The file still carries a line per write: " + lines.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("2000", store.LastWritten(_pairing, _item, Field));
        Assert.Equal("2000", new WrittenValues(directory.Path).LastWritten(_pairing, _item, Field));

        var afterTheRewrite = new WrittenValues(directory.Path).History(_pairing, _item, Field);

        Assert.Equal(WrittenValues.Bound, afterTheRewrite.Count);

        // Both members, because the rewrite builds its lines from what is held
        // rather than copying the ones it replaces. A rewrite carrying the values
        // and dropping what each one replaced would keep every count above
        // correct and silently empty the half a revert reads.
        Assert.Equal("2000", afterTheRewrite[^1].Value);
        Assert.Equal("1999", afterTheRewrite[^1].Previous);
    }

    /// <summary>
    /// The store is not the plugin configuration, and the first condition of #16
    /// says so in those words. It keeps its own file under the plugin's own data
    /// rather than a property on the configuration type, and the file the server
    /// hands to an operator is the other one.
    /// </summary>
    [Fact]
    public void TheStoreIsAFileOfItsOwnAndNotAPropertyOnTheConfiguration()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        store.Record(_pairing, _item, Field, "data rather than a choice", null);

        Assert.Equal(Path.Combine(directory.Path, WrittenValues.FileName), store.Location);
        Assert.True(File.Exists(store.Location));

        Assert.DoesNotContain(
            typeof(PluginConfiguration).GetProperties(),
            property => typeof(IWrittenValues).IsAssignableFrom(property.PropertyType)
                || property.PropertyType == typeof(WrittenValues));
    }

    /// <summary>
    /// The whole point of the write path holding a store: every field an item's
    /// plan wrote is in the record afterwards, keyed by the item and the field,
    /// with the value that was written.
    /// </summary>
    [Fact]
    public async Task EveryFieldAPassWritesIsRecordedAgainstItsItemAndItsField()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);
        var plan = PlanThatWrites();

        await new Applier(new RecordingPlanTarget(), store).ApplyAsync(plan, CancellationToken.None);

        Assert.Equal("the value the plan carried", store.LastWritten(_pairing, _item, Field));
        Assert.Null(store.LastWritten(_pairing, _item, "Tagline"));
    }

    /// <summary>
    /// A row the plan decided against is not recorded. A store that recorded
    /// every row a plan considered would claim this plugin wrote values it
    /// deliberately left alone, and the next pass would read those claims as
    /// permission to overwrite an operator's own text.
    /// </summary>
    [Fact]
    public async Task ARowThePlanDecidedAgainstIsNotRecorded()
    {
        var store = new RecordingWrittenValues();
        var plan = PlanThatWrites();

        await new Applier(new RecordingPlanTarget(), store).ApplyAsync(plan, CancellationToken.None);

        Assert.Equal(new[] { Field }, store.Recorded.Select(row => row.Field).ToArray());
    }

    /// <summary>
    /// An item the write path handed back is not recorded either. The library
    /// never took the value, so a record of it would be this plugin claiming a
    /// write that did not happen, and the next pass would refuse to correct it.
    /// </summary>
    [Fact]
    public async Task AnItemTheWritePathDeferredIsNotRecorded()
    {
        var store = new RecordingWrittenValues();

        var result = await new Applier(new DeferringPlanTarget(), store)
            .ApplyAsync(PlanThatWrites(), CancellationToken.None);

        Assert.Equal(1, result.ItemsDeferred);
        Assert.Equal(0, result.ItemsWritten);
        Assert.Empty(store.Recorded);
    }

    /// <summary>
    /// An applier with nowhere to record is refused as it is built. A default
    /// argument here would be the way a pass that writes a library and records
    /// nothing arrives, which is the failure this whole record exists against.
    /// </summary>
    [Fact]
    public void AnApplierWithNowhereToRecordIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new Applier(new RecordingPlanTarget(), null!));
    }

    /// <summary>
    /// A store with no directory to keep itself in is refused.
    /// </summary>
    [Fact]
    public void AStoreWithNoDirectoryIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new WrittenValues(null!));
    }

    /// <summary>
    /// A store told to keep itself nowhere is refused with the one told to keep
    /// itself in nothing. Accepting an empty name would put the file in whatever
    /// directory the server happened to be running from, which is a place
    /// nobody chose and nobody would think to look.
    /// </summary>
    [Fact]
    public void AStoreWithAnEmptyDirectoryIsRefusedToo()
    {
        Assert.Throws<ArgumentException>(() => new WrittenValues(" "));
    }

    /// <summary>
    /// A blank line in the file is not a lost write. It is what a file ends with
    /// after a write that reached the disk, and reading it as an unreadable
    /// record would report a loss that did not happen.
    /// </summary>
    [Fact]
    public void ABlankLineIsNotCountedAsALostWrite()
    {
        using var directory = new TemporaryDirectory();

        new WrittenValues(directory.Path).Record(_pairing, _item, Field, "the write that finished", null);

        var file = Path.Combine(directory.Path, WrittenValues.FileName);
        File.AppendAllText(file, "\n\n", new UTF8Encoding(false));

        var reopened = new WrittenValues(directory.Path);

        Assert.Equal(0, reopened.Unreadable);
        Assert.Equal("the write that finished", reopened.LastWritten(_pairing, _item, Field));
    }

    /// <summary>
    /// A line that is readable JSON and names no field is counted as unreadable
    /// rather than filed under an empty name. It is the same failure the field
    /// refusal covers arriving from the disk instead of from a caller, and a
    /// store that filed it would answer a later question about a real field with
    /// it.
    /// </summary>
    [Fact]
    public void ALineNamingNoFieldIsCountedRatherThanFiled()
    {
        using var directory = new TemporaryDirectory();

        new WrittenValues(directory.Path).Record(_pairing, _item, Field, "the write that finished", null);

        var file = Path.Combine(directory.Path, WrittenValues.FileName);
        File.AppendAllText(file, "{\"Item\":\"aaaaaaaa-0000-0000-0000-000000000001\",\"Value\":\"orphaned\"}\n", new UTF8Encoding(false));
        File.AppendAllText(file, "null\n", new UTF8Encoding(false));

        var reopened = new WrittenValues(directory.Path);

        Assert.Equal(2, reopened.Unreadable);
        Assert.Equal("the write that finished", reopened.LastWritten(_pairing, _item, Field));
    }

    /// <summary>
    /// A field with no name is refused on all three members rather than answered.
    /// A store that filed a value under an empty name would answer a later
    /// question about a real field with it.
    /// </summary>
    [Fact]
    public void AFieldWithNoNameIsRefusedRatherThanAnswered()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        Assert.Throws<ArgumentException>(() => store.Record(_pairing, _item, " ", "a value", null));
        Assert.Throws<ArgumentException>(() => store.LastWritten(_pairing, _item, " "));
        Assert.Throws<ArgumentException>(() => store.History(_pairing, _item, " "));
    }

    /// <summary>
    /// Says what it holds, so a message an operator reads names the file and the
    /// bound rather than a count on its own.
    /// </summary>
    [Fact]
    public void TheStoreSaysWhatItHolds()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        store.Record(_pairing, _item, Field, "one", null);

        var said = store.ToString();

        Assert.Contains(WrittenValues.FileName, said, StringComparison.Ordinal);
        Assert.Contains(WrittenValues.Bound.ToString(CultureInfo.InvariantCulture), said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The value that was replaced is recorded beside the value that was
    /// written, on the same write. Everything downstream of the record needs the
    /// pair rather than either half: a conflict log entry an operator can read
    /// without remembering what their overview used to say, and a revert with
    /// something to put back.
    /// </summary>
    [Fact]
    public async Task TheValueThatWasThereBeforeIsRecordedBesideTheValueWritten()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        await new Applier(new RecordingPlanTarget(), store)
            .ApplyAsync(PlanThatOverwrites(), CancellationToken.None);

        var written = Assert.Single(store.History(_pairing, _item, Field));

        Assert.Equal("what the peer holds", written.Value);
        Assert.Equal("what this server held", written.Previous);
    }

    /// <summary>
    /// The near miss, and the reason the write path passes what it replaced
    /// rather than the store looking it up. A store deriving the previous value
    /// from its own newest entry answers with what this plugin wrote last time,
    /// which is right on a field nobody touched in between and wrong on exactly
    /// the field this record exists for.
    /// </summary>
    /// <remarks>
    /// The arrangement is the one the whole store is about: this plugin wrote a
    /// value, somebody here edited it afterwards, and a later pass overwrites the
    /// edit. What the second write replaced is the operator's text. A lookup
    /// would record the plugin's own earlier value instead, and the edit would be
    /// gone from the only place that could have shown it.
    /// </remarks>
    [Fact]
    public async Task WhatWasReplacedIsWhatTheLibraryHeldRatherThanWhatThisPluginWroteLastTime()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        store.Record(_pairing, _item, Field, "what this plugin wrote last pass", null);

        await new Applier(new RecordingPlanTarget(), store).ApplyAsync(
            PlanThatOverwrites(local: "what the operator typed afterwards"),
            CancellationToken.None);

        Assert.Equal(
            new string?[] { null, "what the operator typed afterwards" },
            store.History(_pairing, _item, Field).Select(written => written.Previous).ToArray());
    }

    /// <summary>
    /// A write onto a field that held nothing records that it replaced nothing.
    /// The empty history is what says no record exists, so an entry carrying a
    /// null previous value is a write that replaced an absence rather than a
    /// write whose previous value was never captured.
    /// </summary>
    [Fact]
    public async Task AWriteOntoAnEmptyFieldRecordsThatItReplacedNothing()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        await new Applier(new RecordingPlanTarget(), store)
            .ApplyAsync(PlanThatWrites(), CancellationToken.None);

        var written = Assert.Single(store.History(_pairing, _item, Field));

        Assert.Equal("the value the plan carried", written.Value);
        Assert.Null(written.Previous);
        Assert.Empty(store.History(_pairing, _item, "Tagline"));
    }

    /// <summary>
    /// What was replaced outlives the instance that recorded it. A store keeping
    /// the pair in memory and writing half of it to the file would answer every
    /// case above and lose the previous value at the first restart, which is the
    /// one moment nobody is watching.
    /// </summary>
    [Fact]
    public void WhatWasReplacedSurvivesTheInstanceThatRecordedIt()
    {
        using var directory = new TemporaryDirectory();

        new WrittenValues(directory.Path)
            .Record(_pairing, _item, Field, "written before the restart", "replaced before the restart");

        var written = Assert.Single(new WrittenValues(directory.Path).History(_pairing, _item, Field));

        Assert.Equal("written before the restart", written.Value);
        Assert.Equal("replaced before the restart", written.Previous);
    }

    /// <summary>
    /// What happens to the rows of a pairing that no longer exists: they stay,
    /// and nothing reads them as another pairing's.
    /// </summary>
    /// <remarks>
    /// A pairing identifier is derived from the two servers' public keys and a
    /// revocation is terminal, so a pairing established after one carries a
    /// different identifier. The rows of the ended pairing are therefore inert
    /// rather than misleading: the key that would reach them is one nothing asks
    /// about again.
    /// <para>
    /// They are also not deleted, and this asserts that in the direction that
    /// will change. Removing them is #61, which is a thing an operator asks for
    /// and is told the count of, and a store quietly dropping them at the next
    /// restart would have nothing left to report when they did ask.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheRowsOfAPairingThatEndedAreKeptAndAreNotAnotherPairingsRows()
    {
        using var directory = new TemporaryDirectory();
        var thePairingThatFollowed = new Guid("dddddddd-0000-0000-0000-000000000004");

        new WrittenValues(directory.Path)
            .Record(_pairing, _item, Field, "written under the pairing that ended", "what it replaced");

        var afterRestart = new WrittenValues(directory.Path);

        Assert.Empty(afterRestart.History(thePairingThatFollowed, _item, Field));
        Assert.Null(afterRestart.LastWritten(thePairingThatFollowed, _item, Field));

        var kept = Assert.Single(afterRestart.History(_pairing, _item, Field));

        Assert.Equal("written under the pairing that ended", kept.Value);
        Assert.Equal("what it replaced", kept.Previous);
    }

    /// <summary>
    /// Runs one field through the planner with the record the store holds for
    /// it, which is what a reader will do once there is one to build an
    /// observation from.
    /// </summary>
    private static PlannedChange PlanOneField(string? local, string? peer, IWrittenValues store)
    {
        var item = new ItemObservation
        {
            LocalItemId = _item,
            PeerItemId = new Guid("bbbbbbbb-0000-0000-0000-000000000002"),
            Kind = AKindEveryGroupHolds,
        };

        item.Fields.Add(new FieldObservation
        {
            Field = Field,
            LocalValue = local,
            PeerValue = peer,
            LastWrittenByThisPlugin = store.LastWritten(_pairing, _item, Field),
            FieldLockedHere = false,
            FieldLockedOnPeer = false,
        });

        var request = new PlanRequest { PairingId = _pairing, Direction = SyncDirection.TwoWay };
        request.Items.Add(item);

        return Assert.Single(Assert.Single(Planner.Plan(request).Items).Changes);
    }

    /// <summary>
    /// A plan carrying one row that writes and one that does not, so a case can
    /// tell what the write path recorded from what it merely considered.
    /// </summary>
    private static Plan PlanThatWrites()
    {
        var plan = new Plan { PairingId = _pairing, Direction = SyncDirection.TwoWay };

        var item = new ItemPlan
        {
            LocalItemId = _item,
            PeerItemId = new Guid("bbbbbbbb-0000-0000-0000-000000000002"),
            Kind = AKindEveryGroupHolds,
            LastSavedWhenPlanned = "a stamp",
        };

        item.Changes.Add(new PlannedChange
        {
            Field = Field,
            LocalValue = null,
            PeerValue = "the value the plan carried",
            Disposition = PlanDisposition.Decided,
            Outcome = ConflictOutcome.TakePeer,
            Rule = "local-value-absent",
            Writes = true,
            ValueToWrite = "the value the plan carried",
            Reason = "there is nothing here to lose",
        });

        item.Changes.Add(new PlannedChange
        {
            Field = "Tagline",
            LocalValue = "ours",
            PeerValue = "ours",
            Disposition = PlanDisposition.Decided,
            Outcome = ConflictOutcome.KeepLocal,
            Rule = "values-agree",
            Writes = false,
            ValueToWrite = null,
            Reason = "there is nothing to decide",
        });

        plan.Items.Add(item);
        return plan;
    }

    /// <summary>
    /// A plan carrying one row that overwrites a value this server already
    /// holds, so a case can tell what a write replaced from what it wrote.
    /// </summary>
    private static Plan PlanThatOverwrites(string? local = "what this server held")
    {
        var plan = new Plan { PairingId = _pairing, Direction = SyncDirection.TwoWay };

        var item = new ItemPlan
        {
            LocalItemId = _item,
            PeerItemId = new Guid("bbbbbbbb-0000-0000-0000-000000000002"),
            Kind = AKindEveryGroupHolds,
            LastSavedWhenPlanned = "a stamp",
        };

        item.Changes.Add(new PlannedChange
        {
            Field = Field,
            LocalValue = local,
            PeerValue = "what the peer holds",
            Disposition = PlanDisposition.Decided,
            Outcome = ConflictOutcome.TakePeer,
            Rule = "local-unchanged-since-this-plugin-wrote-it",
            Writes = true,
            ValueToWrite = "what the peer holds",
            Reason = "nobody here has touched it since this plugin wrote it",
        });

        plan.Items.Add(item);
        return plan;
    }

    /// <summary>
    /// A write path that hands every item back, so a case can ask what the store
    /// holds after a pass that wrote nothing.
    /// </summary>
    private sealed class DeferringPlanTarget : IPlanTarget
    {
        public Task WriteAsync(ItemPlan item, CancellationToken cancellationToken)
        {
            throw new ItemChangedSincePlannedException("Something else wrote this item between the two halves of the pass.");
        }
    }

    /// <summary>
    /// A directory that exists for one case and is gone afterwards.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "metadata-sync-store-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
