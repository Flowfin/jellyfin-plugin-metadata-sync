using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The account this plugin keeps of what it decided, and what its bound costs.
/// </summary>
/// <remarks>
/// The point of the bound is not that the file stays small. It is that an
/// account which has lost the beginning of itself has to be able to say so, and
/// a store that dropped entries and kept no way of knowing how many would leave
/// a surface reporting a clean number over an incomplete account. That is the
/// failure #48's third condition and #66's fourth are both about, so the cases
/// here spend most of their effort on the losing rather than on the keeping.
/// <para>
/// Each case gets a directory of its own and removes it afterwards, for the
/// reason the other store's cases do: a store is a file, and a suite sharing one
/// has cases that pass in the order somebody ran them.
/// </para>
/// </remarks>
public class ConflictLogTests
{
    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid _other = new("cccccccc-0000-0000-0000-000000000004");
    private static readonly Guid _item = new("aaaaaaaa-0000-0000-0000-000000000001");

    /// <summary>
    /// A pairing nothing was decided for has no account, and the empty list is
    /// what says so rather than a row saying nothing happened.
    /// </summary>
    [Fact]
    public void APairingNothingWasDecidedForHasNoAccount()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        Assert.Empty(log.Entries(_pairing));
        Assert.Equal(0, log.Dropped(_pairing));
    }

    /// <summary>
    /// A decision is read back by an instance built over the same directory
    /// afterwards, with every column it carried. A log that did not survive a
    /// restart would be an account an operator loses at the moment a server is
    /// restarted to fix whatever they were reading it about.
    /// </summary>
    [Fact]
    public void ADecisionIsReadBackByASecondInstance()
    {
        using var directory = new TemporaryDirectory();
        var at = new DateTimeOffset(2026, 8, 29, 4, 15, 0, TimeSpan.Zero);

        new ConflictLog(directory.Path).Record(_pairing, Decision("Overview", "Ours", "Theirs", at));

        var entry = Assert.Single(new ConflictLog(directory.Path).Entries(_pairing));

        Assert.Equal(_item, entry.Item);
        Assert.Equal("Overview", entry.Field);
        Assert.Equal("Ours", entry.LocalValue.Text);
        Assert.Equal("Theirs", entry.PeerValue.Text);
        Assert.Equal("peer-field-locked", entry.Rule);
        Assert.Equal(ConflictOutcome.Refuse, entry.Outcome);
        Assert.Equal(SyncDirection.TwoWay, entry.Direction);
        Assert.Equal(at, entry.At);
    }

    /// <summary>
    /// A value that was cut is read back as a value that was cut. The flag is
    /// what stops two overviews cut to the same length reading as agreement, and
    /// a store that kept the text and lost the flag would remove exactly that.
    /// </summary>
    [Fact]
    public void AValueThatWasCutIsReadBackAsOne()
    {
        using var directory = new TemporaryDirectory();

        new ConflictLog(directory.Path).Record(
            _pairing,
            Decision("Overview", new string('A', ShownValue.DisplayBound + 1), "Theirs", DateTimeOffset.UnixEpoch));

        var entry = Assert.Single(new ConflictLog(directory.Path).Entries(_pairing));

        Assert.True(entry.LocalValue.Truncated);
        Assert.Equal(ShownValue.DisplayBound, entry.LocalValue.Text?.Length);
        Assert.False(entry.PeerValue.Truncated);
    }

    /// <summary>
    /// A field that held nothing is read back as a field that held nothing,
    /// rather than as one holding an empty text. The two are different answers
    /// to why a field did not change and an operator cannot recover the
    /// difference from a log that collapsed them.
    /// </summary>
    [Fact]
    public void AnAbsenceIsReadBackAsAnAbsence()
    {
        using var directory = new TemporaryDirectory();

        new ConflictLog(directory.Path).Record(
            _pairing,
            Decision("Overview", local: null, peer: string.Empty, DateTimeOffset.UnixEpoch));

        var entry = Assert.Single(new ConflictLog(directory.Path).Entries(_pairing));

        Assert.Null(entry.LocalValue.Text);
        Assert.Equal(string.Empty, entry.PeerValue.Text);
    }

    /// <summary>
    /// One pairing's account is its own. A bound shared between pairings would
    /// let one household's first pass push the other's account out, which is why
    /// the pairing is the whole of the key here.
    /// </summary>
    [Fact]
    public void EachPairingHasItsOwnAccount()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        log.Record(_pairing, Decision("Overview", "Ours", "Theirs", DateTimeOffset.UnixEpoch));
        log.Record(_other, Decision("Tagline", "Ours", "Theirs", DateTimeOffset.UnixEpoch));

        Assert.Equal("Overview", Assert.Single(log.Entries(_pairing)).Field);
        Assert.Equal("Tagline", Assert.Single(log.Entries(_other)).Field);
    }

    /// <summary>
    /// The bound keeps the newest and drops the oldest, and the account says how
    /// many it dropped. Both halves in one case, because a store that kept the
    /// right entries and reported nothing lost is the failure this bound is
    /// dangerous without.
    /// </summary>
    [Fact]
    public void TheBoundKeepsTheNewestAndSaysHowManyItDropped()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        for (var i = 0; i < ConflictLog.Bound + 12; i++)
        {
            log.Record(_pairing, Decision(Numbered(i), "Ours", "Theirs", DateTimeOffset.UnixEpoch));
        }

        var entries = log.Entries(_pairing);

        Assert.Equal(ConflictLog.Bound, entries.Count);
        Assert.Equal(Numbered(12), entries[0].Field);
        Assert.Equal(Numbered(ConflictLog.Bound + 11), entries[^1].Field);
        Assert.Equal(12, log.Dropped(_pairing));
    }

    /// <summary>
    /// What the bound dropped is still reported after a restart. The store's own
    /// file is the only thing that could remember it, and this is the leg that
    /// says it does rather than resetting to a clean count on the first read.
    /// </summary>
    [Fact]
    public void WhatTheBoundDroppedSurvivesARestart()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        for (var i = 0; i < ConflictLog.Bound + 12; i++)
        {
            log.Record(_pairing, Decision(Numbered(i), "Ours", "Theirs", DateTimeOffset.UnixEpoch));
        }

        var reopened = new ConflictLog(directory.Path);

        Assert.Equal(ConflictLog.Bound, reopened.Entries(_pairing).Count);
        Assert.Equal(12, reopened.Dropped(_pairing));
    }

    /// <summary>
    /// What the bound dropped is still reported after the file has been
    /// rewritten. This is the case a tally kept beside the entries would fail:
    /// the rewrite drops the superseded lines, and a count that lived in them
    /// would come back as nothing lost.
    /// </summary>
    /// <remarks>
    /// The rewrite is provoked rather than asserted about. It happens once the
    /// file is carrying enough superseded lines to be worth the whole-file cost,
    /// and every entry the bound drops leaves one, so recording past the bound
    /// far enough reaches it. That the file was rewritten is read off the file
    /// rather than off the store: a file still carrying every line has more
    /// lines than the bound allows.
    /// </remarks>
    [Fact]
    public void WhatTheBoundDroppedSurvivesTheFileBeingRewritten()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);
        var recorded = ConflictLog.Bound + 700;

        for (var i = 0; i < recorded; i++)
        {
            log.Record(_pairing, Decision(Numbered(i), "Ours", "Theirs", DateTimeOffset.UnixEpoch));
        }

        var lines = File.ReadAllLines(Path.Combine(directory.Path, "conflict-log.jsonl"))
            .Count(line => line.Length > 0);

        Assert.True(
            lines < recorded,
            "The file still carries every line written, so it was never rewritten and this case is not about what it says it is: " + lines);

        var reopened = new ConflictLog(directory.Path);

        Assert.Equal(ConflictLog.Bound, reopened.Entries(_pairing).Count);
        Assert.Equal(recorded - ConflictLog.Bound, reopened.Dropped(_pairing));
    }

    /// <summary>
    /// A line that did not reach the disk whole is dropped and counted rather
    /// than refused. A log that will not open after a power cut is a log an
    /// operator meets at the moment they need it most.
    /// </summary>
    [Fact]
    public void AShortLastLineIsCountedRatherThanRefused()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "conflict-log.jsonl");

        new ConflictLog(directory.Path).Record(_pairing, Decision("Overview", "Ours", "Theirs", DateTimeOffset.UnixEpoch));

        File.AppendAllText(path, "{\"Pairing\":\"cccccccc-0000-0000-0000-0000000");

        var reopened = new ConflictLog(directory.Path);

        Assert.Single(reopened.Entries(_pairing));
        Assert.Equal(1, reopened.Unreadable);
    }

    /// <summary>
    /// A line that is readable text and is not a row is counted rather than
    /// kept, and each of the three ways of being that is counted.
    /// </summary>
    /// <remarks>
    /// This is a different state from a line that did not reach the disk whole,
    /// which stops being readable at all. These parse and then say nothing: a
    /// line naming nothing, a decision about no field, and a decision carrying
    /// no position in the log. The last is the one that matters most here,
    /// because a row admitted with no position would be counted as held while
    /// contributing nothing to how far the log had got, and the account would
    /// then report fewer entries dropped than it had dropped.
    /// </remarks>
    [Fact]
    public void ALineThatParsesAndIsNotARowIsCountedRatherThanKept()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "conflict-log.jsonl");

        new ConflictLog(directory.Path).Record(_pairing, Decision("Overview", "Ours", "Theirs", DateTimeOffset.UnixEpoch));

        File.AppendAllText(
            path,
            "null\n"
            + "{\"Pairing\":\"cccccccc-0000-0000-0000-000000000003\",\"Reached\":2,\"Field\":\"  \"}\n"
            + "{\"Pairing\":\"cccccccc-0000-0000-0000-000000000003\",\"Reached\":0,\"Field\":\"Overview\"}\n");

        var reopened = new ConflictLog(directory.Path);

        Assert.Single(reopened.Entries(_pairing));
        Assert.Equal(3, reopened.Unreadable);
        Assert.Equal(0, reopened.Dropped(_pairing));
    }

    /// <summary>
    /// A removal takes one pairing's account and leaves the others where they
    /// are, including how far they had got. A removal that forgot the second
    /// would answer correctly until the next restart and then report every other
    /// pairing's account as complete.
    /// </summary>
    [Fact]
    public void ARemovalTakesOnePairingAndLeavesTheOthersCountingCorrectly()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        for (var i = 0; i < ConflictLog.Bound + 5; i++)
        {
            log.Record(_other, Decision(Numbered(i), "Ours", "Theirs", DateTimeOffset.UnixEpoch));
        }

        log.Record(_pairing, Decision("Overview", "Ours", "Theirs", DateTimeOffset.UnixEpoch));

        Assert.Equal(1, log.Remove(_pairing));
        Assert.Empty(log.Entries(_pairing));

        var reopened = new ConflictLog(directory.Path);

        Assert.Empty(reopened.Entries(_pairing));
        Assert.Equal(ConflictLog.Bound, reopened.Entries(_other).Count);
        Assert.Equal(5, reopened.Dropped(_other));
    }

    /// <summary>
    /// A pairing whose account was removed is a pairing nothing was decided for
    /// again, rather than one reported as having lost everything it held.
    /// </summary>
    [Fact]
    public void ARemovedAccountIsNotReportedAsDropped()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        log.Record(_pairing, Decision("Overview", "Ours", "Theirs", DateTimeOffset.UnixEpoch));
        log.Remove(_pairing);

        Assert.Equal(0, log.Dropped(_pairing));
        Assert.Equal(0, new ConflictLog(directory.Path).Dropped(_pairing));
    }

    /// <summary>
    /// The report an operator is shown says what is held, in sentences rather
    /// than in type names, and a value that was cut is said to have been cut.
    /// A sentence showing the first two hundred characters of an overview as
    /// though they were the whole of it is the failure the flag exists against,
    /// in the one place an operator reads.
    /// </summary>
    [Fact]
    public void TheReportSaysWhatIsHeldAndSaysWhatWasCut()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        log.Record(
            _pairing,
            Decision("Overview", new string('A', ShownValue.DisplayBound + 1), "Theirs", DateTimeOffset.UnixEpoch));

        var holding = log.Holding(_pairing);

        Assert.Equal(nameof(ConflictLog), holding.Store);
        Assert.NotEmpty(holding.Held);
        Assert.Contains("(cut)", Assert.Single(holding.Rows), StringComparison.Ordinal);
    }

    /// <summary>
    /// A pairing this log holds nothing for answers with no rows rather than
    /// declining to answer, so a report assembled from every store tells an
    /// operator the truth rather than less than it in the direction that
    /// reassures.
    /// </summary>
    [Fact]
    public void APairingWithNoAccountAnswersWithNoRows()
    {
        using var directory = new TemporaryDirectory();

        var holding = new ConflictLog(directory.Path).Holding(_pairing);

        Assert.Empty(holding.Rows);
        Assert.Equal(0, new ConflictLog(directory.Path).Remove(_pairing));
    }

    /// <summary>
    /// The two named answers are written as their names rather than as their
    /// numbers. A member added to either moves the numbers under a file an
    /// earlier build wrote, and the direction is a type this plan expects to
    /// gain a member.
    /// </summary>
    [Fact]
    public void TheNamedAnswersAreWrittenAsNamesRatherThanNumbers()
    {
        using var directory = new TemporaryDirectory();

        new ConflictLog(directory.Path).Record(_pairing, Decision("Overview", "Ours", "Theirs", DateTimeOffset.UnixEpoch));

        var line = File.ReadAllText(Path.Combine(directory.Path, "conflict-log.jsonl"));

        Assert.Contains(nameof(ConflictOutcome.Refuse), line, StringComparison.Ordinal);
        Assert.Contains(nameof(SyncDirection.TwoWay), line, StringComparison.Ordinal);
    }

    /// <summary>
    /// A plugin that has been installed and has decided nothing leaves no file,
    /// so an operator asking what is on their disk is told about a file that is
    /// there rather than one this plugin created to hold nothing.
    /// </summary>
    [Fact]
    public void ALogThatWasNeverWrittenToLeavesNoFile()
    {
        using var directory = new TemporaryDirectory();

        _ = new ConflictLog(directory.Path);

        Assert.False(File.Exists(Path.Combine(directory.Path, "conflict-log.jsonl")));
    }

    /// <summary>
    /// A log with no directory to keep itself in is refused rather than built
    /// over whatever the working directory happens to be.
    /// </summary>
    [Fact]
    public void ALogWithNoDirectoryIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new ConflictLog(null!));
        Assert.Throws<ArgumentException>(() => new ConflictLog("  "));
    }

    /// <summary>
    /// A decision that is not there is refused rather than kept as a row with
    /// nothing in it.
    /// </summary>
    [Fact]
    public void ADecisionThatIsNotThereIsRefused()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        Assert.Throws<ArgumentNullException>(() => log.Record(_pairing, null!));
    }

    /// <summary>
    /// The sentence an operator reads names the file and what is in it.
    /// </summary>
    [Fact]
    public void TheStoreSaysWhereItIsAndWhatItHolds()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        log.Record(_pairing, Decision("Overview", "Ours", "Theirs", DateTimeOffset.UnixEpoch));

        Assert.Equal(Path.Combine(directory.Path, "conflict-log.jsonl"), log.Location);
        Assert.Contains("1 decision(s)", log.ToString(), StringComparison.Ordinal);
    }

    private static string Numbered(int index) =>
        "Field" + index.ToString(CultureInfo.InvariantCulture);

    private static ConflictEntry Decision(string field, string? local, string? peer, DateTimeOffset at) => new()
    {
        Item = _item,
        Field = field,
        LocalValue = ShownValue.Of(local),
        PeerValue = ShownValue.Of(peer),
        Rule = "peer-field-locked",
        Outcome = ConflictOutcome.Refuse,
        Direction = SyncDirection.TwoWay,
        At = at,
    };

    /// <summary>
    /// A directory that exists for one case and is gone afterwards.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "metadata-sync-log-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

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
