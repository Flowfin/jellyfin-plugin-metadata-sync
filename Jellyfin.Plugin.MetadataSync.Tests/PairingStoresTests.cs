using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.MetadataSync.Store;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// What this plugin holds for one pairing, and what happens when an operator
/// asks for it to be gone.
/// </summary>
/// <remarks>
/// The condition these are for is the one that rots quietest: every store the
/// plugin owns contributes to the report. A case naming the stores it expects
/// passes today and passes after a sixth store is added, which is exactly when
/// the report stops being true. So the set is derived from the tree in two
/// directions here - every source that persists is a store of this shape, and
/// every store of this shape is registered - and the report itself is asserted
/// to drop none of what it was given.
/// </remarks>
public class PairingStoresTests
{
    private const string Field = "Overview";

    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid _anotherPairing = new("dddddddd-0000-0000-0000-000000000004");
    private static readonly Guid _item = new("aaaaaaaa-0000-0000-0000-000000000001");

    private static readonly string[] _twoStoresInOrder = { "AQuietStore", "ALoudStore" };
    private static readonly int[] _noneAndTwo = { 0, 2 };
    private static readonly string[] _theWrittenRow =
    {
        "Overview on item aaaaaaaa-0000-0000-0000-000000000001: wrote \"what the peer holds\", replacing \"what this server held\"",
    };

    private static readonly string[] _theEmptyRow =
    {
        "Overview on item aaaaaaaa-0000-0000-0000-000000000001: wrote \"\", replacing nothing",
    };

    private static readonly string[] _twoSubstitutedStores = { "AFirstStore", "ASecondStore" };

    private static readonly string[] _callsThatWriteToADisk =
    {
        "FileStream",
        "StreamWriter",
        "File.Write",
        "File.Create",
        "File.AppendAll",
    };

    /// <summary>
    /// Every store the plugin owns contributes to the report, including a store
    /// holding nothing for the pairing that was asked about. A report that left
    /// an empty store out would tell an operator less than the truth in the
    /// direction that reassures.
    /// </summary>
    [Fact]
    public void EveryStoreContributesToTheReportIncludingTheOnesHoldingNothing()
    {
        var quiet = new AStoreOfItsOwn("AQuietStore", "nothing about this pairing");
        var loud = new AStoreOfItsOwn("ALoudStore", "two rows about this pairing");

        loud.Add(_pairing, AStoreOfItsOwn.Row(1));
        loud.Add(_pairing, AStoreOfItsOwn.Row(2));

        var report = new PairingStores(new IPairingStore[] { quiet, loud }).Report(_pairing);

        Assert.Equal(_twoStoresInOrder, report.Holdings.Select(holding => holding.Store).ToArray());
        Assert.Equal(_noneAndTwo, report.Holdings.Select(holding => holding.Count).ToArray());
        Assert.Equal(2, report.Count);
    }

    /// <summary>
    /// The real store contributes what it holds, per item and per field, with
    /// what each write replaced. This is the row an operator reads, so it is
    /// asserted as a sentence rather than as a count.
    /// </summary>
    [Fact]
    public void TheStoreOfWhatWasWrittenReportsItsRows()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        store.Record(_pairing, _item, Field, "what the peer holds", "what this server held");

        var holding = Assert.Single(new PairingStores(new IPairingStore[] { store }).Report(_pairing).Holdings);

        Assert.Equal(nameof(WrittenValues), holding.Store);
        Assert.Equal(_theWrittenRow, holding.Rows.ToArray());
    }

    /// <summary>
    /// A value that was empty is said as empty and an absent one is said as an
    /// absence. An operator cannot recover that difference from the document
    /// afterwards, so the document has to carry it.
    /// </summary>
    [Fact]
    public void AFieldThatHeldNothingIsSaidAsNothingRatherThanAsAnEmptyValue()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        store.Record(_pairing, _item, Field, string.Empty, null);

        var holding = Assert.Single(new PairingStores(new IPairingStore[] { store }).Report(_pairing).Holdings);

        Assert.Equal(_theEmptyRow, holding.Rows.ToArray());
    }

    /// <summary>
    /// The document carries every row it counted. A report stating a number and
    /// handing over a summary lets an operator believe they have been given what
    /// the plugin holds when they have been given a number.
    /// </summary>
    [Fact]
    public void TheDocumentCarriesEveryRowItCounted()
    {
        using var directory = new TemporaryDirectory();
        var written = new WrittenValues(directory.Path);
        var other = new AStoreOfItsOwn("AStoreOfItsOwn", "rows of its own");

        written.Record(_pairing, _item, Field, "one", "what one replaced");
        written.Record(_pairing, _item, "Tagline", "two", null);
        other.Add(_pairing, AStoreOfItsOwn.Row(1));
        other.Add(_pairing, AStoreOfItsOwn.Row(2));
        other.Add(_anotherPairing, AStoreOfItsOwn.Row(3));

        var report = new PairingStores(new IPairingStore[] { written, other }).Report(_pairing);
        var document = report.Document();

        Assert.Equal(4, report.Count);

        foreach (var row in report.Holdings.SelectMany(holding => holding.Rows))
        {
            Assert.Contains(row, document, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(AStoreOfItsOwn.Row(3), document, StringComparison.Ordinal);
        Assert.Contains(_pairing.ToString(), document, StringComparison.Ordinal);
        Assert.Contains("4 row(s) in total, across 2 store(s).", document, StringComparison.Ordinal);
    }

    /// <summary>
    /// The document says what it is carrying before it carries any of it, and it
    /// says that removing these records is not the same act as putting a value
    /// back. Both are things an operator decides on before they read the rows.
    /// </summary>
    [Fact]
    public void TheDocumentSaysWhatItIsAndWhatRemovalDoesNotDo()
    {
        var report = new PairingStores(Array.Empty<IPairingStore>()).Report(_pairing);
        var document = report.Document();

        var says = document.IndexOf("text somebody typed into a library", StringComparison.Ordinal);
        var rows = document.IndexOf("row(s) in total", StringComparison.Ordinal);

        Assert.True(says >= 0, "The document does not say what it is carrying.");
        Assert.True(says < rows, "The document says what it is carrying after the rows rather than before them.");
        Assert.Contains("does not change the library", document, StringComparison.Ordinal);
    }

    /// <summary>
    /// Removal deletes exactly that pairing's records. The second pairing is
    /// untouched, and it is asserted through a restart, because a store that
    /// removed a pairing from memory and left the file alone would answer this
    /// correctly until the next one.
    /// </summary>
    [Fact]
    public void RemovalDeletesOnePairingAndLeavesTheOtherWhereItWas()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);

        store.Record(_pairing, _item, Field, "the one going", "what it replaced");
        store.Record(_anotherPairing, _item, Field, "the one staying", "what that replaced");

        var removed = new PairingStores(new IPairingStore[] { store }).Remove(_pairing);

        Assert.Equal(1, removed.Count);
        Assert.Empty(store.History(_pairing, _item, Field));
        Assert.Equal("the one staying", store.LastWritten(_anotherPairing, _item, Field));

        var afterRestart = new WrittenValues(directory.Path);

        Assert.Empty(afterRestart.History(_pairing, _item, Field));
        Assert.Equal("the one staying", afterRestart.LastWritten(_anotherPairing, _item, Field));
        Assert.Equal(0, afterRestart.Unreadable);
    }

    /// <summary>
    /// What comes back from a removal is what went, not what is left. An
    /// operator is owed the list they were about to be shown, and what is left
    /// is nothing.
    /// </summary>
    [Fact]
    public void ARemovalAnswersWithWhatItRemoved()
    {
        using var directory = new TemporaryDirectory();
        var store = new WrittenValues(directory.Path);
        var stores = new PairingStores(new IPairingStore[] { store });

        store.Record(_pairing, _item, Field, "the value", "what it replaced");

        var removed = stores.Remove(_pairing);

        Assert.Contains("wrote \"the value\", replacing \"what it replaced\"", removed.Document(), StringComparison.Ordinal);
        Assert.Equal(0, stores.Report(_pairing).Count);
    }

    /// <summary>
    /// Every store is asked, including the ones holding nothing for this
    /// pairing. A removal that skipped the quiet stores would be right today and
    /// wrong the first time a store answered a count of zero for a reason other
    /// than being empty.
    /// </summary>
    [Fact]
    public void EveryStoreIsAskedToRemoveEvenWhereItHoldsNothing()
    {
        var quiet = new AStoreOfItsOwn("AQuietStore", "nothing about this pairing");
        var loud = new AStoreOfItsOwn("ALoudStore", "one row about this pairing");

        loud.Add(_pairing, AStoreOfItsOwn.Row(1));

        new PairingStores(new IPairingStore[] { quiet, loud }).Remove(_pairing);

        Assert.Equal(1, quiet.RemovalsAsked);
        Assert.Equal(1, loud.RemovalsAsked);
    }

    /// <summary>
    /// A store that reports one number and removes another is refused rather
    /// than reported. Removing more than was reported deletes rows of a pairing
    /// nobody asked about; removing fewer leaves rows an operator has been told
    /// are gone, which is the worse of the two because it is a false assurance.
    /// </summary>
    [Fact]
    public void AStoreThatRemovesADifferentNumberThanItReportedIsRefused()
    {
        var store = new AStoreOfItsOwn("AStoreThatDisagreesWithItself", "one row");

        store.Add(_pairing, AStoreOfItsOwn.Row(1));
        store.AnswersRemovalWith = 4;

        var refusal = Assert.Throws<StoreRemovedADifferentNumberException>(
            () => new PairingStores(new IPairingStore[] { store }).Remove(_pairing));

        Assert.Contains("AStoreThatDisagreesWithItself", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("1 row(s)", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("removed 4", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A caller that forgot to hand the stores over is refused, and one that
    /// hands over none is not. The two produce the same answer - this plugin
    /// holds nothing - and only one of them is true.
    /// </summary>
    [Fact]
    public void NoStoresIsAnAnswerAndNoSetOfStoresIsARefusal()
    {
        Assert.Throws<ArgumentNullException>(() => new PairingStores(null!));
        Assert.Equal(0, new PairingStores(Array.Empty<IPairingStore>()).Report(_pairing).Count);
    }

    /// <summary>
    /// Every plugin source that writes to a disk declares a store of this shape.
    /// This is the derived half of the first condition: a sixth store that keeps
    /// something of its own and does not answer for a pairing reddens here
    /// rather than being missing from a report nobody re-reads.
    /// </summary>
    /// <remarks>
    /// The scan is the one <see cref="StorageStatementTests"/> already makes over
    /// the same sources, and its bound is the same: it is a line scan, so a call
    /// that writes to a disk spelled some other way is invisible to it. What it
    /// catches is the file that persists in the way this plugin persists.
    /// </remarks>
    [Fact]
    public void EverySourceThatPersistsDeclaresAStoreOfThisShape()
    {
        var stores = Implementations().Select(type => type.Name).ToList();

        Assert.NotEmpty(SourcesThatWriteToADisk());
        Assert.Empty(SourcesThatWriteToADisk()
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !stores.Contains(name, StringComparer.Ordinal))
            .ToList());
    }

    /// <summary>
    /// Every store of this shape is registered, so the container hands the
    /// report all of them. A store that implements the interface and is never
    /// registered is the same failure as one that never implemented it, arriving
    /// one step later.
    /// </summary>
    /// <remarks>
    /// The concrete type is what the descriptors are read for rather than the
    /// interface, because a factory descriptor says nothing about what it will
    /// build. Registering the store as itself and forwarding the interfaces to it
    /// is what makes that readable, and it is the same arrangement that stops two
    /// stores being built over one file.
    /// </remarks>
    [Fact]
    public void EveryStoreOfThisShapeIsRegistered()
    {
        var services = new ServiceCollection();

        new PluginServiceRegistrator().RegisterServices(services, null!);

        var registered = services.Select(descriptor => descriptor.ServiceType).ToList();
        var implementations = Implementations().ToList();

        Assert.NotEmpty(implementations);
        Assert.Empty(implementations.Where(type => !registered.Contains(type)).ToList());
        Assert.Equal(
            implementations.Count,
            services.Count(descriptor => descriptor.ServiceType == typeof(IPairingStore)));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(PairingStores));
    }

    /// <summary>
    /// The report the container builds is over the stores the container holds.
    /// The registration is read through the container rather than asserted from
    /// the descriptors, because what the report is given is what
    /// <c>GetServices</c> answers with and nothing else.
    /// </summary>
    /// <remarks>
    /// The stores are substituted in rather than resolved, because the real one
    /// is built over the plugin's data folder and reaching it needs the static
    /// instance no test may set. What is asserted here is the wiring between the
    /// report and the set, which is the part that could be wrong without a
    /// server.
    /// </remarks>
    [Fact]
    public void TheReportIsOverEveryStoreTheContainerHolds()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IPairingStore>(new AStoreOfItsOwn("AFirstStore", "one thing"));
        services.AddSingleton<IPairingStore>(new AStoreOfItsOwn("ASecondStore", "another thing"));
        services.AddSingleton(provider => new PairingStores(provider.GetServices<IPairingStore>()));

        var report = services.BuildServiceProvider().GetRequiredService<PairingStores>().Report(_pairing);

        Assert.Equal(_twoSubstitutedStores, report.Holdings.Select(holding => holding.Store).ToArray());
    }

    /// <summary>
    /// The count a holding states is the number of rows it carries, because it
    /// is derived from them. A store answering a count it worked out separately
    /// is how a report says eleven and hands over nine.
    /// </summary>
    [Fact]
    public void ACountIsTheRowsRatherThanANumberBesideThem()
    {
        Assert.Empty(typeof(PairingHolding)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name == nameof(PairingHolding.Count) && property.CanWrite)
            .ToList());
    }

    /// <summary>
    /// The stores this plugin declares, found rather than listed.
    /// </summary>
    /// <returns>The concrete types implementing the store shape.</returns>
    private static IEnumerable<Type> Implementations()
    {
        return typeof(Plugin).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IPairingStore).IsAssignableFrom(type));
    }

    /// <summary>
    /// The plugin sources that name a call which writes to a disk.
    /// </summary>
    /// <returns>The files.</returns>
    private static IReadOnlyList<string> SourcesThatWriteToADisk()
    {
        var sources = Path.Combine(AppContext.BaseDirectory, "plugin-sources");

        Assert.True(Directory.Exists(sources), $"The plugin sources were not copied to {sources}");

        return Directory.EnumerateFiles(sources, "*.cs", SearchOption.AllDirectories)
            .Where(file => _callsThatWriteToADisk.Any(call => File.ReadAllText(file).Contains(call, StringComparison.Ordinal)))
            .ToList();
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
                "metadata-sync-pairing-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

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
