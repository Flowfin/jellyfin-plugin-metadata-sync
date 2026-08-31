using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// A plugin installed again over a store an earlier installation left behind
/// resumes from it, rather than syncing the library from the beginning.
/// </summary>
/// <remarks>
/// This is the seam between the two halves the suite already holds, and it is
/// the seam rather than either half that an operator meets.
/// <see cref="PassProgressTests"/> asks whether a record survives the process
/// that wrote it, against a real directory and with no pass anywhere near it.
/// <see cref="PassResumptionTests"/> asks what a pass does with a record while
/// it runs, against a double whose own remarks say it is the shape of the store
/// and not the store. Neither of them runs a pass over a record that came off
/// the disk, so a pass that read its progress from somewhere other than the
/// store, or a store whose file the pass never opened, is green in both.
/// <para>
/// A reinstall is exactly that crossing. The installation that was interrupted
/// is gone, its instances with it, and the only thing the new one inherits is
/// the file. So the store here is built twice over one directory, and the second
/// build is the reinstalled plugin: nothing is handed from the first pass to the
/// second except what reached the disk.
/// </para>
/// <para>
/// The interruption is stood in for the way the neighbouring suite stands it in,
/// because a case cannot kill the process it is running in. What is different
/// here is that the record it stops after is a real one on a real file, so the
/// state the resume is asked about is the state a disk would be in.
/// </para>
/// <para>
/// What this does not reach. Nothing here installs, uninstalls or reinstalls
/// anything: no server is started and no plugin is loaded, which is the headless
/// policy in <c>docs/testing.md</c>. What is exercised is the property a
/// reinstall depends on - a pass over a store instance that read the file and
/// nothing else - and not the act. It also says nothing about the store's
/// version being checked on the way in, which is #59.
/// </para>
/// </remarks>
public class ReinstallResumptionTests
{
    private static readonly Guid _pairing = new("cccccccc-0000-0000-0000-000000000003");

    /// <summary>
    /// The condition read straight: a pass is interrupted, the installation it
    /// ran in ends, and the pass that runs after the plugin is installed again
    /// covers what is left rather than the library.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AReinstalledPluginResumesFromTheStoreOnDisk()
    {
        using var directory = new TemporaryDirectory();
        var items = Items(4);

        var interrupted = new TargetThatStops(Guid.Empty);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new Pass(
                    new Applier(interrupted, new RecordingWrittenValues()),
                    new StoreThatStops(new PassProgress(directory.Path), items[1]),
                    TimeProvider.System, PassClock.NotReached)
                .RunAsync(RequestFor(items), CancellationToken.None));

        // The reinstalled plugin. It shares nothing with the run above but the
        // directory, which is what a reinstall shares with the installation
        // before it.
        var afterwards = new TargetThatStops(Guid.Empty);
        var resumed = await new Pass(
                new Applier(afterwards, new RecordingWrittenValues()),
                new PassProgress(directory.Path),
                TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.Equal(
            new[] { items[2], items[3] },
            afterwards.Written.Select(item => item.LocalItemId).ToArray());
        Assert.Equal(2, resumed.ItemsAlreadyDone);
        Assert.Equal(2, resumed.ItemsWritten);

        // The union is every item once, which is the half the count above cannot
        // say on its own.
        Assert.Equal(
            items.Order().ToList(),
            interrupted.Written.Concat(afterwards.Written)
                .Select(item => item.LocalItemId).Order().ToList());
    }

    /// <summary>
    /// The near miss, and the case that says the one above is about the file.
    /// An installation that came up over an empty directory - which is what
    /// deleting the store at uninstall would leave, and is the behaviour
    /// <c>docs/lifecycle.md</c> decides against - writes every item again.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AReinstallThatFoundNoStoreWritesTheWholeLibraryAgain()
    {
        using var kept = new TemporaryDirectory();
        using var blank = new TemporaryDirectory();
        var items = Items(4);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new Pass(
                    new Applier(new TargetThatStops(Guid.Empty), new RecordingWrittenValues()),
                    new StoreThatStops(new PassProgress(kept.Path), items[1]),
                    TimeProvider.System, PassClock.NotReached)
                .RunAsync(RequestFor(items), CancellationToken.None));

        var afterwards = new TargetThatStops(Guid.Empty);
        var blind = await new Pass(
                new Applier(afterwards, new RecordingWrittenValues()),
                new PassProgress(blank.Path),
                TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.Equal(items, afterwards.Written.Select(item => item.LocalItemId).ToList());
        Assert.Equal(0, blind.ItemsAlreadyDone);

        // And the directory that was kept still holds the record, so the two
        // runs differ in which store they were pointed at and in nothing else.
        Assert.Equal(
            new[] { items[0], items[1] }.Order().ToList(),
            new PassProgress(kept.Path).CompletedItems(_pairing).Order().ToList());
    }

    /// <summary>
    /// A pass that ran to the end leaves nothing on the disk for the next
    /// installation to skip. Without this a plugin that finished one pass and
    /// was then reinstalled would consider its whole library already done,
    /// which is the failure that looks like a plugin doing nothing at all.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AReinstallAfterAFinishedPassConsidersEveryItemAgain()
    {
        using var directory = new TemporaryDirectory();
        var items = Items(3);

        await new Pass(
                new Applier(new TargetThatStops(Guid.Empty), new RecordingWrittenValues()),
                new PassProgress(directory.Path),
                TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.Empty(new PassProgress(directory.Path).CompletedItems(_pairing));

        var afterwards = new TargetThatStops(Guid.Empty);
        var again = await new Pass(
                new Applier(afterwards, new RecordingWrittenValues()),
                new PassProgress(directory.Path),
                TimeProvider.System, PassClock.NotReached)
            .RunAsync(RequestFor(items), CancellationToken.None);

        Assert.Equal(items, afterwards.Written.Select(item => item.LocalItemId).ToList());
        Assert.Equal(0, again.ItemsAlreadyDone);
    }

    private static List<Guid> Items(int count) =>
        Enumerable.Range(1, count)
            .Select(n => new Guid(string.Format(CultureInfo.InvariantCulture, "aaaaaaaa-0000-0000-0000-{0:D12}", n)))
            .ToList();

    private static PlanRequest RequestFor(IEnumerable<Guid> items)
    {
        var request = new PlanRequest
        {
            PairingId = _pairing,
            Direction = SyncDirection.TwoWay,
        };

        foreach (var id in items)
        {
            var item = new ItemObservation
            {
                LocalItemId = id,
                PeerItemId = id,
                Kind = "Movie",
                ItemLockedHere = false,
            };

            item.Fields.Add(new FieldObservation
            {
                Field = "Overview",
                LocalValue = null,
                PeerValue = "what the peer said",
                LastWrittenByThisPlugin = null,
                FieldLockedHere = false,
                FieldLockedOnPeer = false,
            });

            request.Items.Add(item);
        }

        return request;
    }

    /// <summary>
    /// The real store, which stops the pass after it has recorded one item.
    /// </summary>
    /// <remarks>
    /// It stands in for the process ending, and it delegates rather than
    /// pretending: every question and every record goes to a
    /// <see cref="PassProgress"/> over a real directory, so what is on the disk
    /// when it throws is what would have been on the disk had the process died
    /// there. A double that held the rows itself would answer the resume from
    /// memory and the case would pass with the file empty, which is the reading
    /// this whole file exists to refuse.
    /// </remarks>
    private sealed class StoreThatStops : IPassProgress
    {
        private readonly PassProgress _store;
        private readonly Guid _item;
        private bool _spent;

        public StoreThatStops(PassProgress store, Guid item)
        {
            _store = store;
            _item = item;
        }

        public void Completed(Guid pairingId, Guid itemId)
        {
            _store.Completed(pairingId, itemId);

            if (!_spent && itemId == _item)
            {
                _spent = true;
                throw new OperationCanceledException();
            }
        }

        public IReadOnlyCollection<Guid> CompletedItems(Guid pairingId) => _store.CompletedItems(pairingId);

        public int Cleared(Guid pairingId) => _store.Cleared(pairingId);
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
                "metadata-sync-reinstall-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

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
