using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The record of how far a pass got, against a real directory.
/// </summary>
/// <remarks>
/// What is asked here is the half a double cannot answer: whether a record
/// survives the process that wrote it. The pass that reads it back runs after
/// the one that was interrupted has died, so every case below builds a second
/// instance over the same directory rather than asking the first one again.
/// </remarks>
public class PassProgressTests
{
    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid _other = new("dddddddd-0000-0000-0000-000000000004");
    private static readonly Guid _item = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid _second = new("aaaaaaaa-0000-0000-0000-000000000002");

    /// <summary>
    /// A store with nowhere to keep itself is refused.
    /// </summary>
    [Fact]
    public void AStoreWithNoDirectoryIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new PassProgress(null!));
    }

    /// <summary>
    /// A pairing nothing was recorded for answers with nothing, which is the
    /// ordinary state and is what says no pass was interrupted.
    /// </summary>
    [Fact]
    public void APairingWithNoInterruptedPassHasNothingRecorded()
    {
        using var directory = new TemporaryDirectory();

        Assert.Empty(new PassProgress(directory.Path).CompletedItems(_pairing));
    }

    /// <summary>
    /// The whole point of the store: an instance built over the directory an
    /// earlier one wrote answers what that one recorded.
    /// </summary>
    [Fact]
    public void WhatOnePassRecordedIsReadBackByTheNextOne()
    {
        using var directory = new TemporaryDirectory();

        var interrupted = new PassProgress(directory.Path);
        interrupted.Completed(_pairing, _item);
        interrupted.Completed(_pairing, _second);

        Assert.Equal(
            new[] { _item, _second }.Order().ToList(),
            new PassProgress(directory.Path).CompletedItems(_pairing).Order().ToList());
    }

    /// <summary>
    /// The same item recorded twice is one item. A pass interrupted between an
    /// item's write and its record writes that item again when it resumes, so a
    /// repeated line is an ordinary consequence of the ordering rather than a
    /// defect.
    /// </summary>
    [Fact]
    public void TheSameItemRecordedTwiceIsOneItem()
    {
        using var directory = new TemporaryDirectory();

        var store = new PassProgress(directory.Path);
        store.Completed(_pairing, _item);
        store.Completed(_pairing, _item);

        Assert.Equal(new[] { _item }, store.CompletedItems(_pairing).ToArray());
        Assert.Equal(new[] { _item }, new PassProgress(directory.Path).CompletedItems(_pairing).ToArray());
    }

    /// <summary>
    /// Clearing a pairing's progress survives a restart. A store that removed
    /// the rows from memory and appended nothing would answer correctly until
    /// the next restart and then read every cleared row back off the disk, which
    /// is a finished pass turning into an interrupted one.
    /// </summary>
    [Fact]
    public void AClearedPassStaysClearedAcrossARestart()
    {
        using var directory = new TemporaryDirectory();

        var store = new PassProgress(directory.Path);
        store.Completed(_pairing, _item);

        Assert.Equal(1, store.Cleared(_pairing));
        Assert.Empty(new PassProgress(directory.Path).CompletedItems(_pairing));
    }

    /// <summary>
    /// Clearing one pairing leaves another pairing's interrupted pass where it
    /// was. Two pairings can be interrupted at once and each one's resume is its
    /// own.
    /// </summary>
    [Fact]
    public void ClearingOnePairingLeavesTheOthersProgress()
    {
        using var directory = new TemporaryDirectory();

        var store = new PassProgress(directory.Path);
        store.Completed(_pairing, _item);
        store.Completed(_other, _second);

        store.Cleared(_pairing);

        Assert.Empty(new PassProgress(directory.Path).CompletedItems(_pairing));
        Assert.Equal(new[] { _second }, new PassProgress(directory.Path).CompletedItems(_other).ToArray());
    }

    /// <summary>
    /// A pass that finished and an operator removing what this plugin holds for
    /// a pairing reach one act. They are asserted to be one rather than left to
    /// be assumed, because two implementations of a removal are two chances for
    /// one of them to leave rows behind.
    /// </summary>
    [Fact]
    public void RemovingIsTheSameActAsClearing()
    {
        using var directory = new TemporaryDirectory();

        var store = new PassProgress(directory.Path);
        store.Completed(_pairing, _item);

        Assert.Equal(1, store.Remove(_pairing));
        Assert.Equal(0, store.Cleared(_pairing));
        Assert.Empty(new PassProgress(directory.Path).CompletedItems(_pairing));
    }

    /// <summary>
    /// What an operator is shown for one pairing is a row per item, in the
    /// store's own name, so the report an operator reads names this store as
    /// well as the one beside it.
    /// </summary>
    [Fact]
    public void WhatIsHeldForOnePairingIsReported()
    {
        using var directory = new TemporaryDirectory();

        var store = new PassProgress(directory.Path);
        store.Completed(_pairing, _item);

        var holding = store.Holding(_pairing);

        Assert.Equal(nameof(PassProgress), holding.Store);
        Assert.Equal(1, holding.Count);
        Assert.Contains(_item.ToString(), Assert.Single(holding.Rows), StringComparison.Ordinal);
        Assert.Equal(0, store.Holding(_other).Count);
    }

    /// <summary>
    /// A line that never finished, which is what a pass killed part way through
    /// a write leaves behind, is dropped on the next read and counted rather
    /// than thrown. A store that refused to open after a power cut would have
    /// turned an interruption into a pass that can never resume at all.
    /// </summary>
    [Fact]
    public void ALineThatNeverFinishedIsDroppedAndCounted()
    {
        using var directory = new TemporaryDirectory();

        new PassProgress(directory.Path).Completed(_pairing, _item);

        var path = Path.Combine(directory.Path, PassProgress.FileName);
        File.AppendAllText(path, "{\"Pairing\":\"cccccccc-0000-0000", new UTF8Encoding(false));

        var reopened = new PassProgress(directory.Path);

        Assert.Equal(1, reopened.Unreadable);
        Assert.Equal(new[] { _item }, reopened.CompletedItems(_pairing).ToArray());
    }

    /// <summary>
    /// A line that reads back as nothing at all is dropped and counted too. It
    /// is a different shape from the short line above - the reader returns no
    /// row rather than refusing the text - and the two arrive by different
    /// routes, so one leg cannot stand for both.
    /// </summary>
    [Fact]
    public void ALineThatReadsBackAsNothingIsDroppedAndCounted()
    {
        using var directory = new TemporaryDirectory();

        new PassProgress(directory.Path).Completed(_pairing, _item);

        File.AppendAllText(Path.Combine(directory.Path, PassProgress.FileName), "null\n", new UTF8Encoding(false));

        var reopened = new PassProgress(directory.Path);

        Assert.Equal(1, reopened.Unreadable);
        Assert.Equal(new[] { _item }, reopened.CompletedItems(_pairing).ToArray());
    }

    /// <summary>
    /// A file carrying enough repeated lines is rewritten from what is held. The
    /// repeats are what an interrupted-and-resumed pass leaves, so the file of a
    /// pairing that keeps being interrupted over the same items does not grow
    /// without end.
    /// </summary>
    [Fact]
    public void AFileCarryingEnoughRepeatedLinesIsRewritten()
    {
        using var directory = new TemporaryDirectory();

        var store = new PassProgress(directory.Path);

        for (var n = 0; n < 513; n++)
        {
            store.Completed(_pairing, _item);
        }

        var lines = File.ReadAllLines(Path.Combine(directory.Path, PassProgress.FileName))
            .Where(line => line.Length > 0)
            .ToList();

        Assert.Single(lines);
        Assert.Equal(new[] { _item }, new PassProgress(directory.Path).CompletedItems(_pairing).ToArray());
    }

    /// <summary>
    /// Nothing is created by reading. A directory with no store in it is the
    /// state a plugin is installed in, and a read that stamped it would turn
    /// every question about the store into a write.
    /// </summary>
    [Fact]
    public void ReadingTheStoreCreatesNothing()
    {
        using var directory = new TemporaryDirectory();

        new PassProgress(directory.Path).CompletedItems(_pairing);

        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    /// <summary>
    /// The store says what it holds, for a message an operator reads.
    /// </summary>
    [Fact]
    public void TheStoreSaysWhatItHolds()
    {
        using var directory = new TemporaryDirectory();

        var store = new PassProgress(directory.Path);
        store.Completed(_pairing, _item);

        Assert.Contains(PassProgress.FileName, store.ToString(), StringComparison.Ordinal);
        Assert.Contains(PassProgress.FileName, store.Location, StringComparison.Ordinal);
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
                "metadata-sync-progress-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

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
