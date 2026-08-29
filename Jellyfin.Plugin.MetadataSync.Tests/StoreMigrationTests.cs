using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The chain that steps a store directory forward one format at a time, and
/// what a directory looks like after a step that did not finish.
/// </summary>
/// <remarks>
/// One format has existed, so the chain this build carries is empty and every
/// refusal in the mechanism is unreachable from the public constructor. That is
/// the state #59 asks for the mechanism to be built in, and it is also the state
/// in which a mechanism can be shipped without anything ever having run it. The
/// arrangements below run it, against formats a fixture chain declares, so the
/// first execution of a step is here rather than on somebody's library.
/// <para>
/// The case they are all about is the one the stamp cannot answer on its own. A
/// stamp says which shape a directory is in; it does not move the directory to
/// another shape, and a build that meets an older shape and reads it anyway is
/// the same silent loss a downgrade produces, arriving from the other side.
/// </para>
/// </remarks>
public class StoreMigrationTests
{
    /// <summary>
    /// The chain carries one step for every format below the one this build
    /// reads. It is an empty comparison today, and it is the leg that bites the
    /// day somebody raises the current format: a build whose chain cannot reach
    /// its own number refuses every store the build before it wrote, and nothing
    /// else in this tree would say so before a release.
    /// </summary>
    [Fact]
    public void TheChainCarriesOneStepForEveryFormatBelowTheOneThisBuildReads()
    {
        Assert.Equal(
            Enumerable.Range(StoreFormat.Earliest, StoreFormat.Current - StoreFormat.Earliest).ToList(),
            StoreFormat.Chain.Select(step => step.From).OrderBy(from => from).ToList());
    }

    /// <summary>
    /// A step moves a directory by exactly one format. The end it finishes at is
    /// derived rather than given, so a chain assembled out of these cannot name a
    /// pair that skips a format, and the skipped one is the shape nobody wrote a
    /// step for.
    /// </summary>
    [Fact]
    public void AStepMovesADirectoryByExactlyOneFormat()
    {
        var step = new FormatStep(4, _ => { });

        Assert.Equal(4, step.From);
        Assert.Equal(5, step.To);
    }

    /// <summary>
    /// A step with no change to make is refused where it is written rather than
    /// where it would run. A chain assembled at start-up and run at the first
    /// upgrade is a chain whose hole is found on somebody's installation.
    /// </summary>
    [Fact]
    public void AStepWithNoChangeToMakeIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new FormatStep(1, null!));
    }

    /// <summary>
    /// A step starting before any format that has existed is refused. There is
    /// no shape for it to be written against, so it could only ever be a step
    /// somebody meant to start somewhere else.
    /// </summary>
    [Fact]
    public void AStepStartingBeforeAnyFormatThatHasExistedIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FormatStep(StoreFormat.Earliest - 1, _ => { }));
    }

    /// <summary>
    /// The store this build reads has nothing to step forward, which is the
    /// answer on every installation today. It is asserted through the public
    /// constructor because that is the one the server uses, and a mechanism
    /// proved only through its own seam is one nobody has run the way it ships.
    /// </summary>
    [Fact]
    public void TheStoreThisBuildReadsHasNothingToStepForward()
    {
        using var directory = new TemporaryDirectory();

        Assert.Equal(0, new StoreFormat(directory.Path).Migrate());
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path));
    }

    /// <summary>
    /// A directory already in the format being read is left alone, stamp
    /// included. Rewriting it would move the file's timestamp on every start and
    /// say nothing new, and copying it would spend the whole store's size on a
    /// run with no step to make.
    /// </summary>
    [Fact]
    public void ADirectoryAlreadyInThatFormatIsNotTouched()
    {
        using var directory = new TemporaryDirectory();
        Stamped(directory, 2);
        Wrote(directory, "written-values.jsonl", "one row");

        var before = Snapshot(directory);

        Assert.Equal(0, new StoreFormat(directory.Path, 2, Chain(1, 2)).Migrate());
        Assert.Equal(before, Snapshot(directory));
        Assert.Empty(Beside(directory));
    }

    /// <summary>
    /// The steps run in format order and the stamp follows them, so a directory
    /// two formats behind arrives at the current one having passed through the
    /// shape in between rather than skipping it.
    /// </summary>
    [Fact]
    public void ADirectoryStepsForwardOneFormatAtATimeAndTheStampFollows()
    {
        using var directory = new TemporaryDirectory();
        Stamped(directory, 1);
        Wrote(directory, "written-values.jsonl", "one row");

        var order = new List<int>();

        var chain = new[]
        {
            Step(2, order, "two"),
            Step(1, order, "one"),
        };

        Assert.Equal(2, new StoreFormat(directory.Path, 3, chain).Migrate());
        Assert.Equal(new List<int> { 1, 2 }, order);
        Assert.Equal("one row one two", Read(directory, "written-values.jsonl"));
        Assert.Equal(Said(3), Read(directory, StoreFormat.FileName));
        Assert.Empty(Beside(directory));
    }

    /// <summary>
    /// A step that throws leaves the directory exactly as it was, stamp and rows
    /// included. This is the condition the mechanism exists for: a store that is
    /// half of one format and half of the next is a store no reader can place,
    /// and no build after it could tell that from the shape it was written in.
    /// </summary>
    [Fact]
    public void AStepThatThrowsLeavesTheDirectoryAsItWas()
    {
        using var directory = new TemporaryDirectory();
        Stamped(directory, 1);
        Wrote(directory, "written-values.jsonl", "one row");

        var before = Snapshot(directory);

        var chain = new[]
        {
            Step(1, new List<int>(), "one"),
            new FormatStep(2, _ => throw new InvalidOperationException("the step could not finish")),
        };

        var refused = Assert.Throws<InvalidOperationException>(
            () => new StoreFormat(directory.Path, 3, chain).Migrate());

        Assert.Equal("the step could not finish", refused.Message);
        Assert.Equal(before, Snapshot(directory));
        Assert.Empty(Beside(directory));
    }

    /// <summary>
    /// A format no step starts from is refused, and the refusal happens before
    /// anything is copied. A chain with a hole in it would otherwise run the
    /// steps it has and stamp the directory with a number those steps did not
    /// reach.
    /// </summary>
    [Fact]
    public void AFormatNoStepStartsFromIsRefusedBeforeAnythingIsCopied()
    {
        using var directory = new TemporaryDirectory();
        Stamped(directory, 1);
        Wrote(directory, "written-values.jsonl", "one row");

        var before = Snapshot(directory);
        var order = new List<int>();

        var refused = Assert.Throws<StoreFormatRefusedException>(
            () => new StoreFormat(directory.Path, 3, new[] { Step(1, order, "one") }).Migrate());

        Assert.Contains("start from format 2", refused.Message, StringComparison.Ordinal);
        Assert.Empty(order);
        Assert.Equal(before, Snapshot(directory));
        Assert.Empty(Beside(directory));
    }

    /// <summary>
    /// Two steps starting from one format are the same refusal as none. Which of
    /// them decided the shape would depend on the order the chain was written in,
    /// and a store that took the other one is indistinguishable afterwards.
    /// </summary>
    [Fact]
    public void TwoStepsFromOneFormatAreRefusedTheSameWayAsNone()
    {
        using var directory = new TemporaryDirectory();
        Stamped(directory, 1);

        var order = new List<int>();

        var refused = Assert.Throws<StoreFormatRefusedException>(
            () => new StoreFormat(directory.Path, 2, new[] { Step(1, order, "one"), Step(1, order, "other") }).Migrate());

        Assert.Contains("2 of the steps", refused.Message, StringComparison.Ordinal);
        Assert.Empty(order);
        Assert.Empty(Beside(directory));
    }

    /// <summary>
    /// A stamp from the future is refused before any step runs, which is the
    /// refusal <see cref="StoreFormatTests"/> holds for a read, held here for the
    /// route that would otherwise copy the directory first and discover it after.
    /// </summary>
    [Fact]
    public void AStampFromTheFutureIsRefusedBeforeAnyStepRuns()
    {
        using var directory = new TemporaryDirectory();
        Stamped(directory, 4);

        var order = new List<int>();

        Assert.Throws<StoreFormatRefusedException>(
            () => new StoreFormat(directory.Path, 3, new[] { Step(1, order, "one"), Step(2, order, "two") }).Migrate());

        Assert.Empty(order);
        Assert.Empty(Beside(directory));
    }

    /// <summary>
    /// A store under a directory of its own arrives with the rest of it. A
    /// migration that copied the top level and left the directories under it
    /// would answer correctly for today's store and destroy the first one that
    /// nests.
    /// </summary>
    [Fact]
    public void EverythingUnderTheDirectoryStepsForwardWithIt()
    {
        using var directory = new TemporaryDirectory();
        Stamped(directory, 1);
        Directory.CreateDirectory(Path.Combine(directory.Path, "conflicts"));
        Wrote(directory, Path.Combine("conflicts", "log.jsonl"), "one entry");

        Assert.Equal(1, new StoreFormat(directory.Path, 2, Chain(1, 2)).Migrate());
        Assert.Equal("one entry one", Read(directory, Path.Combine("conflicts", "log.jsonl")));
        Assert.Empty(Beside(directory));
    }

    /// <summary>
    /// A migration with no chain at all is refused. An empty chain and no chain
    /// are different statements: the first says nothing has to move, and the
    /// second says nobody decided.
    /// </summary>
    [Fact]
    public void AMigrationWithNoChainIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new StoreFormat("directory", 1, null!));
    }

    /// <summary>
    /// A format below the earliest one that has existed is not a format to read.
    /// A build reading one would take every directory in existence for a shape
    /// ahead of it and refuse the lot.
    /// </summary>
    [Fact]
    public void AFormatBelowTheEarliestOneIsNotAFormatToRead()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StoreFormat("directory", StoreFormat.Earliest - 1, Array.Empty<FormatStep>()));
    }

    /// <summary>
    /// A step that appends a word to every file it finds, recording that it ran.
    /// </summary>
    /// <param name="from">The format it starts from.</param>
    /// <param name="order">Where it records having run.</param>
    /// <param name="word">What it appends.</param>
    /// <returns>The step.</returns>
    private static FormatStep Step(int from, List<int> order, string word) =>
        new(from, working =>
        {
            order.Add(from);

            foreach (var file in Directory.GetFiles(working, "*", SearchOption.AllDirectories)
                .Where(file => !string.Equals(Path.GetFileName(file), StoreFormat.FileName, StringComparison.Ordinal)))
            {
                File.WriteAllText(file, File.ReadAllText(file) + " " + word, new UTF8Encoding(false));
            }
        });

    /// <summary>
    /// A chain from one format to another, one step per format.
    /// </summary>
    /// <param name="from">The first format it steps from.</param>
    /// <param name="to">The format it finishes at.</param>
    /// <returns>The chain.</returns>
    private static FormatStep[] Chain(int from, int to) =>
        Enumerable.Range(from, to - from).Select(format => Step(format, new List<int>(), "one")).ToArray();

    /// <summary>
    /// A stamp as this build writes it.
    /// </summary>
    /// <param name="format">The format the stamp declares.</param>
    /// <returns>The bytes.</returns>
    private static string Said(int format) =>
        string.Format(CultureInfo.InvariantCulture, "{{\"format\":{0}}}\n", format);

    private static void Stamped(TemporaryDirectory directory, int format) =>
        File.WriteAllText(
            Path.Combine(directory.Path, StoreFormat.FileName),
            Said(format),
            new UTF8Encoding(false));

    private static void Wrote(TemporaryDirectory directory, string name, string content) =>
        File.WriteAllText(Path.Combine(directory.Path, name), content, new UTF8Encoding(false));

    private static string Read(TemporaryDirectory directory, string name) =>
        File.ReadAllText(Path.Combine(directory.Path, name));

    /// <summary>
    /// Every path under the directory with what is in it, so untouched is a
    /// comparison of bytes rather than of a file count.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The paths and their contents, in order.</returns>
    private static IReadOnlyList<string> Snapshot(TemporaryDirectory directory) =>
        Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Path.GetRelativePath(directory.Path, path) + "=" + File.ReadAllText(path))
            .ToList();

    /// <summary>
    /// What the migration left beside the store directory. A working copy or a
    /// superseded directory still there is a run that did not finish tidying up,
    /// and the next run would meet it.
    /// </summary>
    /// <param name="directory">The store directory.</param>
    /// <returns>The leftovers.</returns>
    private static IReadOnlyList<string> Beside(TemporaryDirectory directory) =>
        Directory.GetDirectories(
                Path.GetDirectoryName(directory.Path)!,
                Path.GetFileName(directory.Path) + ".*")
            .Where(path => !string.Equals(path, directory.Path, StringComparison.Ordinal))
            .ToList();

    /// <summary>
    /// A directory that exists for one case and is gone afterwards.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "metadata-sync-migration-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

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
