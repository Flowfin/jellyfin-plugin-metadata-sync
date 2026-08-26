using System;
using System.Globalization;
using System.IO;
using System.Text;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The stamp that says which format the files in the plugin's store directory
/// are written in, and what happens to a directory this build cannot place.
/// </summary>
/// <remarks>
/// The case these are all about is a downgrade. A newer build writes the store,
/// the operator puts an older build back, and the older build meets a file whose
/// shape it does not know. Every reader in this tree drops what it does not
/// understand, so the loss is silent and the next compaction makes it permanent.
/// What is asserted here is that the older build stops instead.
/// </remarks>
public class StoreFormatTests
{
    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid _item = new("aaaaaaaa-0000-0000-0000-000000000001");

    /// <summary>
    /// A directory with no stamp is the earliest format. Two things produce one,
    /// a plugin that has never written and a directory written before the stamp
    /// existed, and both hold files of that format.
    /// </summary>
    [Fact]
    public void ADirectoryWithNoStampIsTheEarliestFormat()
    {
        using var directory = new TemporaryDirectory();

        Assert.Equal(StoreFormat.Earliest, new StoreFormat(directory.Path).Declared());
    }

    /// <summary>
    /// Reading a directory creates nothing in it. A directory with no store in it
    /// is the state a plugin is installed in, and a read that stamped it would
    /// turn every question about the store into a write.
    /// </summary>
    [Fact]
    public void ReadingADirectoryLeavesNothingBehindInIt()
    {
        using var directory = new TemporaryDirectory();
        var format = new StoreFormat(directory.Path);

        format.Declared();

        Assert.False(File.Exists(format.Location));
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    /// <summary>
    /// A store that has been built over a directory and has written nothing
    /// leaves it empty, stamp included. This is the same property
    /// <see cref="WrittenValuesTests"/> holds for the store's own file, held for
    /// the file this change adds beside it.
    /// </summary>
    [Fact]
    public void AStoreThatHasWrittenNothingLeavesNoStamp()
    {
        using var directory = new TemporaryDirectory();

        _ = new WrittenValues(directory.Path);

        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    /// <summary>
    /// A directory this plugin has written to says which format it is in. The
    /// stamp arrives with the first write rather than with the first question.
    /// </summary>
    [Fact]
    public void ADirectoryThisPluginHasWrittenToSaysWhichFormatItIsIn()
    {
        using var directory = new TemporaryDirectory();

        new WrittenValues(directory.Path).Record(_pairing, _item, "Overview", "a value", null);

        var format = new StoreFormat(directory.Path);

        Assert.True(File.Exists(format.Location));
        Assert.Equal(StoreFormat.Current, format.Declared());
    }

    /// <summary>
    /// A stamp already there is left exactly as it is. Rewriting it would move
    /// the file's timestamp on every pass and say nothing new, and a reader
    /// looking at when the store was last stamped would be reading when it was
    /// last written to instead.
    /// </summary>
    [Fact]
    public void AStampAlreadyThereIsLeftExactlyAsItIs()
    {
        using var directory = new TemporaryDirectory();
        var format = new StoreFormat(directory.Path);
        var spelled = "{ \"Format\" : 1 }\n";

        File.WriteAllText(format.Location, spelled, new UTF8Encoding(false));

        var store = new WrittenValues(directory.Path);

        store.Record(_pairing, _item, "Overview", "a value", null);
        store.Record(_pairing, _item, "Tagline", "another", null);

        Assert.Equal(spelled, File.ReadAllText(format.Location));
    }

    /// <summary>
    /// A stamp from the future is refused. This is the downgrade, and it is the
    /// reason this type exists.
    /// </summary>
    [Fact]
    public void AFormatFromTheFutureIsRefused()
    {
        using var directory = new TemporaryDirectory();

        Stamped(directory, StoreFormat.Current + 1);

        var refusal = Assert.Throws<StoreFormatRefusedException>(() => new StoreFormat(directory.Path).Declared());

        Assert.Contains("newer than this build", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The store itself refuses to open over such a directory, which is where the
    /// refusal has to be for it to prevent anything. A refusal only the stamp
    /// knows about is a refusal every caller can walk past.
    /// </summary>
    [Fact]
    public void AStoreOverADirectoryFromTheFutureIsNotOpened()
    {
        using var directory = new TemporaryDirectory();

        Stamped(directory, StoreFormat.Current + 1);

        Assert.Throws<StoreFormatRefusedException>(() => new WrittenValues(directory.Path));
    }

    /// <summary>
    /// Nothing in such a directory is touched. The file the newer build wrote is
    /// byte for byte what it was, because the refusal happens before a line of it
    /// is read and therefore before anything can be written back without what it
    /// carried.
    /// </summary>
    [Fact]
    public void NothingInADirectoryFromTheFutureIsTouched()
    {
        using var directory = new TemporaryDirectory();
        var written = Path.Combine(directory.Path, WrittenValues.FileName);
        var line = "{\"Pairing\":\"cccccccc-0000-0000-0000-000000000003\",\"Item\":\"aaaaaaaa-0000-0000-0000-000000000001\",\"Field\":\"Overview\",\"Value\":\"a value\",\"SomethingThisBuildDoesNotKnow\":\"kept\"}\n";

        File.WriteAllText(written, line, new UTF8Encoding(false));
        Stamped(directory, StoreFormat.Current + 1);

        Assert.Throws<StoreFormatRefusedException>(() => new WrittenValues(directory.Path));
        Assert.Equal(line, File.ReadAllText(written));
        Assert.Equal(Said(StoreFormat.Current + 1), File.ReadAllText(Path.Combine(directory.Path, StoreFormat.FileName)));
    }

    /// <summary>
    /// A stamp that cannot be read is refused rather than read as the earliest
    /// format. Reading it as the earliest is the assumption that fails in the
    /// destroying direction, because a newer file whose stamp was damaged would
    /// then be opened and written back without what it carried.
    /// </summary>
    [Fact]
    public void AStampThatCannotBeReadIsRefused()
    {
        using var directory = new TemporaryDirectory();
        var format = new StoreFormat(directory.Path);

        File.WriteAllText(format.Location, "this is not a stamp", new UTF8Encoding(false));

        var refusal = Assert.Throws<StoreFormatRefusedException>(() => format.Declared());

        Assert.Contains("no store format this build can read", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A stamp below the earliest format is refused by the same site. No build of
    /// this plugin ever wrote one, so it is a damaged stamp rather than an old
    /// store, and the two are told apart by nothing else.
    /// </summary>
    [Fact]
    public void AStampBelowTheEarliestFormatIsRefused()
    {
        using var directory = new TemporaryDirectory();

        Stamped(directory, StoreFormat.Earliest - 1);

        Assert.Throws<StoreFormatRefusedException>(() => new StoreFormat(directory.Path).Declared());
    }

    /// <summary>
    /// A refused stamp is not stamped over. A run that repaired the stamp it just
    /// refused would make the refusal a one-time message and open the directory on
    /// the next start, which is the failure this whole type exists against arriving
    /// one restart later.
    /// </summary>
    [Fact]
    public void ARefusedStampIsNotStampedOver()
    {
        using var directory = new TemporaryDirectory();
        var format = new StoreFormat(directory.Path);

        Stamped(directory, StoreFormat.Current + 1);

        Assert.Throws<StoreFormatRefusedException>(format.Stamp);
        Assert.Equal(Said(StoreFormat.Current + 1), File.ReadAllText(format.Location));
    }

    /// <summary>
    /// The stamp holds nothing for any pairing, and a removal takes nothing away.
    /// It is a store of that shape because it persists and the suite refuses a
    /// source that persists and answers for no pairing, and the honest answer to
    /// both questions is empty.
    /// </summary>
    [Fact]
    public void TheStampHoldsNothingForAnyPairingAndARemovalTakesNothingAway()
    {
        using var directory = new TemporaryDirectory();
        var format = new StoreFormat(directory.Path);

        new WrittenValues(directory.Path).Record(_pairing, _item, "Overview", "a value", null);

        var holding = format.Holding(_pairing);

        Assert.Equal(nameof(StoreFormat), holding.Store);
        Assert.Empty(holding.Rows);
        Assert.Equal(0, holding.Count);
        Assert.Equal(0, format.Remove(_pairing));
        Assert.True(File.Exists(format.Location));
    }

    /// <summary>
    /// A stamp with no directory to be in is refused, so the store's own refusal
    /// of a missing directory is not the only one standing between a caller and a
    /// path built out of nothing.
    /// </summary>
    [Fact]
    public void AStampWithNoDirectoryIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new StoreFormat(null!));
        Assert.Throws<ArgumentException>(() => new StoreFormat("   "));
    }

    /// <summary>
    /// The two constants answer two questions. They are the same number today
    /// because one format has existed, and the leg is here so the day they differ
    /// is a day somebody read this file rather than a day an unstamped directory
    /// was quietly treated as current.
    /// </summary>
    [Fact]
    public void TheEarliestFormatIsNeverAheadOfTheCurrentOne()
    {
        Assert.True(StoreFormat.Earliest <= StoreFormat.Current);
        Assert.True(StoreFormat.Earliest >= 1);
    }

    /// <summary>
    /// A stamp as this build writes it, for the arrangements above that need one
    /// saying something this build did not write.
    /// </summary>
    /// <param name="format">The format the stamp declares.</param>
    /// <returns>The bytes.</returns>
    private static string Said(int format) =>
        string.Format(CultureInfo.InvariantCulture, "{{\"format\":{0}}}\n", format);

    private static void Stamped(TemporaryDirectory directory, int format)
    {
        File.WriteAllText(
            Path.Combine(directory.Path, StoreFormat.FileName),
            Said(format),
            new UTF8Encoding(false));
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
                "metadata-sync-format-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

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
