using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The account leaving this plugin as something an operator can hand over,
/// which is #48's fourth condition.
/// </summary>
/// <remarks>
/// The property worth holding is not that a file is produced. It is that
/// everything the store could still say arrives in it, the counts that say what
/// is missing included, because a file carrying only the rows reads as the whole
/// account to whoever opens it and that is the one reading this register cannot
/// afford.
/// <para>
/// So the round trip below compares the account rather than the text. Two texts
/// that differ by whitespace are the same account, and two accounts that differ
/// by a count nobody carried are not, which is the direction the comparison has
/// to be sensitive in.
/// </para>
/// </remarks>
public class ConflictExportTests
{
    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid _item = new("aaaaaaaa-0000-0000-0000-000000000001");

    /// <summary>
    /// Everything the store holds for a pairing reaches the file, and comes back
    /// out of it with every column it went in with.
    /// </summary>
    [Fact]
    public void AnAccountSurvivesBeingWrittenOutAndReadBack()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);
        var at = new DateTimeOffset(2026, 8, 29, 4, 15, 0, TimeSpan.Zero);

        log.Record(_pairing, Decision("Overview", "Ours", "Theirs", at, rule: "peer-field-locked"));
        log.Record(_pairing, Decision("Tagline", local: null, peer: "From the peer", at, rule: null));

        var read = ConflictExport.Read(ConflictExport.Written(log.Account(_pairing)));

        Assert.Equal(_pairing, read.Pairing);
        Assert.Equal(2, read.Entries.Count);

        Assert.Equal("Overview", read.Entries[0].Field);
        Assert.Equal(_item, read.Entries[0].Item);
        Assert.Equal("Ours", read.Entries[0].LocalValue.Text);
        Assert.Equal("Theirs", read.Entries[0].PeerValue.Text);
        Assert.Equal("peer-field-locked", read.Entries[0].Rule);
        Assert.Equal(ConflictOutcome.Refuse, read.Entries[0].Outcome);
        Assert.Equal(SyncDirection.TwoWay, read.Entries[0].Direction);
        Assert.Equal(at, read.Entries[0].At);

        Assert.Null(read.Entries[1].LocalValue.Text);
        Assert.Null(read.Entries[1].Rule);
    }

    /// <summary>
    /// A value that was cut arrives cut, and says so. A file that carried the
    /// text and lost the flag would let two overviews cut to the same length
    /// read as agreement in the one artefact an operator forwards to somebody
    /// who cannot check.
    /// </summary>
    [Fact]
    public void AValueThatWasCutArrivesSayingItWasCut()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        log.Record(
            _pairing,
            Decision("Overview", new string('A', ShownValue.DisplayBound + 1), "Theirs", DateTimeOffset.UnixEpoch, rule: null));

        var entry = Assert.Single(ConflictExport.Read(ConflictExport.Written(log.Account(_pairing))).Entries);

        Assert.True(entry.LocalValue.Truncated);
        Assert.Equal(ShownValue.DisplayBound, entry.LocalValue.Text?.Length);
        Assert.False(entry.PeerValue.Truncated);
    }

    /// <summary>
    /// What the account can no longer say travels with what it can. This is the
    /// leg the whole export exists for: an operator who exports a log, finds the
    /// field they are arguing about missing and concludes it was never decided
    /// is wrong in the one direction this register cannot afford.
    /// </summary>
    [Fact]
    public void WhatTheAccountLostTravelsWithWhatItKept()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        for (var i = 0; i < ConflictLog.Bound + 7; i++)
        {
            log.Record(_pairing, Decision(Numbered(i), "Ours", "Theirs", DateTimeOffset.UnixEpoch, rule: null));
        }

        var read = ConflictExport.Read(ConflictExport.Written(log.Account(_pairing)));

        Assert.Equal(ConflictLog.Bound, read.Entries.Count);
        Assert.Equal(7, read.Dropped);
        Assert.Equal(ConflictLog.Bound, read.BoundedAt);
        Assert.Equal(0, read.Unreadable);
    }

    /// <summary>
    /// A store carrying lines it could not read says so in the file as well.
    /// Those lines are decisions this plugin took and can no longer show, and a
    /// file reporting a clean account over them is the same failure the bound's
    /// own count exists against.
    /// </summary>
    [Fact]
    public void LinesTheStoreCouldNotReadAreSaidInTheFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "conflict-log.jsonl");

        new ConflictLog(directory.Path).Record(_pairing, Decision("Overview", "Ours", "Theirs", DateTimeOffset.UnixEpoch, rule: null));
        File.AppendAllText(path, "{\"Pairing\":\"cccccccc-0000-0000-0000-0000000");

        var read = ConflictExport.Read(ConflictExport.Written(new ConflictLog(directory.Path).Account(_pairing)));

        Assert.Single(read.Entries);
        Assert.Equal(1, read.Unreadable);
    }

    /// <summary>
    /// A pairing nothing was decided for exports an account with no rows, rather
    /// than nothing at all. An operator who exported and got no file cannot tell
    /// that from a plugin that failed to export.
    /// </summary>
    [Fact]
    public void APairingNothingWasDecidedForStillExportsAnAccount()
    {
        using var directory = new TemporaryDirectory();

        var read = ConflictExport.Read(ConflictExport.Written(new ConflictLog(directory.Path).Account(_pairing)));

        Assert.Equal(_pairing, read.Pairing);
        Assert.Empty(read.Entries);
        Assert.Equal(0, read.Dropped);
    }

    /// <summary>
    /// The file is a file: written to a disk by whoever was handed the text and
    /// read back from there with nothing lost on the way.
    /// </summary>
    /// <remarks>
    /// The plugin chooses no path and this case does the choosing, which is the
    /// shape the condition takes until a surface exists to hand the file over.
    /// What is proved here is that the text is a whole file rather than a
    /// fragment something else has to frame.
    /// </remarks>
    [Fact]
    public void TheTextIsAFileAnOperatorCanTakeAway()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);
        var taken = Path.Combine(directory.Path, "taken-away.json");

        log.Record(_pairing, Decision("Overview", "Ours", "Theirs", DateTimeOffset.UnixEpoch, rule: null));

        File.WriteAllText(taken, ConflictExport.Written(log.Account(_pairing)), new UTF8Encoding(false));

        var read = ConflictExport.Read(File.ReadAllText(taken));

        Assert.Equal("Overview", Assert.Single(read.Entries).Field);
    }

    /// <summary>
    /// The named answers are written as their names in the file too. An operator
    /// reading an outcome as a number, and a reader on a build where a member
    /// was added above it reading a different one, are the two halves of the
    /// same failure.
    /// </summary>
    [Fact]
    public void TheNamedAnswersAreWrittenAsNamesInTheFile()
    {
        using var directory = new TemporaryDirectory();
        var log = new ConflictLog(directory.Path);

        log.Record(_pairing, Decision("Overview", "Ours", "Theirs", DateTimeOffset.UnixEpoch, rule: null));

        var text = ConflictExport.Written(log.Account(_pairing));

        Assert.Contains(nameof(ConflictOutcome.Refuse), text, StringComparison.Ordinal);
        Assert.Contains(nameof(SyncDirection.TwoWay), text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A text that is not an account is refused rather than answered with an
    /// empty one, because an empty account and an unreadable file are the same
    /// object to whoever asked and only one of them means nothing was decided.
    /// </summary>
    [Fact]
    public void ATextThatIsNotAnAccountIsRefused()
    {
        Assert.Throws<JsonException>(() => ConflictExport.Read("null"));
        Assert.Throws<JsonException>(() => ConflictExport.Read("{\"Pairing\":\"not a pairing\"}"));
        Assert.Throws<JsonException>(() => ConflictExport.Read("this is not a file this plugin wrote"));
    }

    /// <summary>
    /// A file with no rows in it at all is refused rather than read as an
    /// account of nothing, which is the shape a truncated download takes.
    /// </summary>
    [Fact]
    public void AFileThatIsMissingWhatAnAccountCarriesIsRefused()
    {
        Assert.Throws<JsonException>(() => ConflictExport.Read("{}"));
    }

    /// <summary>
    /// An account that is not there is refused rather than written out as an
    /// empty file.
    /// </summary>
    [Fact]
    public void AnAccountThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ConflictExport.Written(null!));
    }

    /// <summary>
    /// A text that is not there is refused rather than read as an empty account.
    /// </summary>
    [Fact]
    public void ATextThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ConflictExport.Read(null!));
        Assert.Throws<ArgumentException>(() => ConflictExport.Read("   "));
    }

    private static string Numbered(int index) =>
        "Field" + index.ToString(CultureInfo.InvariantCulture);

    private static ConflictEntry Decision(string field, string? local, string? peer, DateTimeOffset at, string? rule) => new()
    {
        Item = _item,
        Field = field,
        LocalValue = ShownValue.Of(local),
        PeerValue = ShownValue.Of(peer),
        Rule = rule,
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
                "metadata-sync-export-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

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
