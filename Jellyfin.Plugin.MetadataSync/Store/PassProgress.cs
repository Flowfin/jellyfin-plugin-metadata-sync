using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// How far an interrupted pass got, kept in a file of its own beside the
/// plugin's data.
/// </summary>
/// <remarks>
/// It is the second store this plugin owns and it is deliberately the smaller
/// of the two. A line carries a pairing and an item on this server, and that is
/// the whole of it: what a pass wrote is <see cref="WrittenValues"/>'s and lives
/// for as long as the value does, while what a pass has got through is true of
/// one pass and is thrown away when that pass ends. Keying one on the other is
/// wrong in both directions - a resume reading a bounded history would treat an
/// item whose oldest write the bound discarded as an item nothing wrote, and a
/// history keyed by a pass would lose the attribution the moment the pass
/// finished.
/// <para>
/// The file is a line per finished item, appended, for the reason
/// <see cref="WrittenValues"/> appends: a line either reaches the disk whole or
/// it does not, so a pass killed part way through leaves a file whose last line
/// may be short and whose earlier lines are intact. A short last line is dropped
/// on the next read and counted in <see cref="Unreadable"/>, which costs one
/// item being written a second time rather than a store that will not open.
/// </para>
/// <para>
/// There is no bound and there is no need of one. What the file holds is one
/// pass's finished items, it is emptied the moment that pass finishes, and a
/// pass considers the items of the libraries an operator put in it. An
/// interrupted pass therefore leaves at most one pass's worth of identifiers on
/// the disk, and the pass that continues it removes them by finishing.
/// </para>
/// <para>
/// The same item recorded twice is one item. A pass interrupted between an
/// item's write and this record writes that item again when it resumes, so a
/// repeated line is the ordinary consequence of the ordering that keeps a resume
/// safe rather than a defect, and the set is what a reader is answered with.
/// </para>
/// </remarks>
public sealed class PassProgress : IPassProgress, IPairingStore
{
    /// <summary>
    /// The file inside the store directory.
    /// </summary>
    internal const string FileName = "pass-progress.jsonl";

    /// <summary>
    /// How many lines the file may carry beyond the items it is holding before
    /// it is rewritten.
    /// </summary>
    /// <remarks>
    /// The only line this store writes that is not a new item is one recording
    /// an item a resumed pass had already recorded, so the excess this counts is
    /// how often passes have been interrupted and continued over the same items.
    /// It is a floor rather than a ratio for the reason
    /// <see cref="WrittenValues"/> uses one: rewriting on every append pays the
    /// whole-file cost the append exists to avoid.
    /// </remarks>
    private const int CompactionFloor = 512;

    private static readonly JsonSerializerOptions _json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();

    /// <summary>
    /// The items each pairing's interrupted pass has finished with.
    /// </summary>
    /// <remarks>
    /// A set per pairing rather than one set of pairs, because every question
    /// asked of this store names a pairing and the answer is the whole set for
    /// it. The pairing is a component of the key for the reason it is one in
    /// every store here: a revocation is terminal and the identifier that
    /// replaces it is a different one, so rows of an ended pairing cannot be
    /// read as rows of the pairing that followed it.
    /// </remarks>
    private readonly Dictionary<Guid, HashSet<Guid>> _done = new();

    private readonly StoreFormat _format;
    private readonly string _directory;
    private readonly string _path;
    private int _lines;
    private bool _stamped;

    /// <summary>
    /// Initializes a new instance of the <see cref="PassProgress"/> class over a
    /// directory, reading back whatever an earlier run left there.
    /// </summary>
    /// <param name="directory">The directory this plugin keeps its own data in.</param>
    /// <exception cref="ArgumentNullException">There is no directory to keep the store in.</exception>
    /// <exception cref="ArgumentException">The directory is named by nothing but space.</exception>
    /// <remarks>
    /// Reading in the constructor is what makes a restart invisible to a caller,
    /// and on this store it is the whole point of it: the pass that resumes runs
    /// in a process started after the one that was interrupted died. Nothing is
    /// created here, so a plugin that has been installed and has run no pass
    /// leaves no file.
    /// </remarks>
    public PassProgress(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _directory = directory;
        _path = Path.Combine(directory, FileName);

        // Read before a line of the store is, for the reason WrittenValues reads
        // it first: a file written by a newer build would otherwise be dropped to
        // what this build understands and written back that way.
        _format = new StoreFormat(directory);
        _format.Declared();

        Load();
    }

    /// <summary>
    /// Gets the file this store is kept in.
    /// </summary>
    public string Location => _path;

    /// <summary>
    /// Gets how many lines the file carried that could not be read back.
    /// </summary>
    /// <remarks>
    /// Counted rather than swallowed, and what a count above zero costs here is
    /// smaller than what it costs in <see cref="WrittenValues"/>: a lost line is
    /// an item a resumed pass writes again, and writing an item again writes the
    /// values it already holds. It is reported because a number nobody reports
    /// is a number nobody can notice growing.
    /// </remarks>
    public int Unreadable { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Written for somebody reading a list of stores rather than reading this
    /// file, so it says what the rows are about instead of naming the type.
    /// </remarks>
    public string Held => "which items a pass over this pairing had already finished with when it was interrupted";

    /// <inheritdoc />
    public void Completed(Guid pairingId, Guid itemId)
    {
        lock (_gate)
        {
            if (!_done.TryGetValue(pairingId, out var items))
            {
                items = new HashSet<Guid>();
                _done[pairingId] = items;
            }

            items.Add(itemId);

            Append(new Row { Pairing = pairingId, Item = itemId });
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> CompletedItems(Guid pairingId)
    {
        lock (_gate)
        {
            return _done.TryGetValue(pairingId, out var items)
                ? new ReadOnlyCollection<Guid>(items.Order().ToList())
                : Array.Empty<Guid>();
        }
    }

    /// <inheritdoc />
    public int Cleared(Guid pairingId) => Remove(pairingId);

    /// <inheritdoc />
    public PairingHolding Holding(Guid pairingId)
    {
        lock (_gate)
        {
            return new PairingHolding
            {
                Store = nameof(PassProgress),
                Held = Held,
                Rows = _done.TryGetValue(pairingId, out var items)
                    ? items.Order().Select(Sentence).ToList()
                    : new List<string>(),
            };
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The file is rewritten from what is left rather than having lines struck
    /// out of it, for the reason <see cref="WrittenValues"/> rewrites: a store
    /// that removed a pairing from memory and appended nothing would answer
    /// correctly until the next restart and then read every removed row back off
    /// the disk.
    /// <para>
    /// A pass that finishes and an operator who removes what this plugin holds
    /// for a pairing are two callers reaching one act. What separates them is
    /// what they mean by it, which is why <see cref="Cleared"/> exists as a name
    /// a pass can call rather than as a second implementation.
    /// </para>
    /// </remarks>
    public int Remove(Guid pairingId)
    {
        lock (_gate)
        {
            if (!_done.TryGetValue(pairingId, out var items))
            {
                return 0;
            }

            var removed = items.Count;
            _done.Remove(pairingId);

            Compact(_done.Values.Sum(set => set.Count));

            return removed;
        }
    }

    /// <summary>
    /// Says what the store holds, for a message an operator reads.
    /// </summary>
    /// <returns>A sentence naming the file and what is in it.</returns>
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0} holds {1} finished item(s) across {2} interrupted pass(es).",
        _path,
        _done.Values.Sum(set => set.Count),
        _done.Count);

    /// <summary>
    /// One row of this store, said in a sentence.
    /// </summary>
    /// <param name="item">The item on this server.</param>
    /// <returns>The sentence.</returns>
    private static string Sentence(Guid item) => string.Format(
        CultureInfo.InvariantCulture,
        "item {0}: written by a pass that did not finish",
        item);

    /// <summary>
    /// Reads the file back.
    /// </summary>
    /// <remarks>
    /// A line that cannot be read is skipped and counted. It is at the end of
    /// the file after a pass was killed, and anywhere else it is a defect this
    /// type cannot distinguish from that.
    /// </remarks>
    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        foreach (var line in File.ReadLines(_path))
        {
            if (line.Length == 0)
            {
                continue;
            }

            _lines++;

            Row? row;

            try
            {
                row = JsonSerializer.Deserialize<Row>(line, _json);
            }
            catch (JsonException)
            {
                Unreadable++;
                continue;
            }

            if (row is null)
            {
                Unreadable++;
                continue;
            }

            if (!_done.TryGetValue(row.Pairing, out var items))
            {
                items = new HashSet<Guid>();
                _done[row.Pairing] = items;
            }

            items.Add(row.Item);
        }
    }

    /// <summary>
    /// Adds one line to the file, and rewrites the file when it is carrying
    /// enough repeated lines to be worth the cost.
    /// </summary>
    /// <param name="row">The line to add.</param>
    private void Append(Row row)
    {
        Directory.CreateDirectory(_directory);

        // Stamped at the first write rather than at construction, for the reason
        // WrittenValues stamps there: a plugin that has been installed and has
        // written nothing leaves an empty directory rather than a file.
        if (!_stamped)
        {
            _format.Stamp();
            _stamped = true;
        }

        File.AppendAllText(_path, JsonSerializer.Serialize(row, _json) + "\n", new UTF8Encoding(false));
        _lines++;

        var held = _done.Values.Sum(set => set.Count);

        if (_lines - held >= CompactionFloor)
        {
            Compact(held);
        }
    }

    /// <summary>
    /// Writes what is held to a new file and moves it over the old one.
    /// </summary>
    /// <param name="held">How many items are left after whatever prompted this.</param>
    /// <remarks>
    /// Written beside the store and moved onto it, so a compaction interrupted
    /// half way leaves the file it was replacing untouched.
    /// </remarks>
    private void Compact(int held)
    {
        var replacement = _path + ".compacting";

        var lines = _done
            .SelectMany(entry => entry.Value.Order().Select(item => new Row { Pairing = entry.Key, Item = item }))
            .Select(row => JsonSerializer.Serialize(row, _json));

        File.WriteAllText(replacement, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        File.Move(replacement, _path, overwrite: true);
        _lines = held;
    }

    /// <summary>
    /// What one line of the file carries. It is a type of its own so the member
    /// names on the disk are decided here rather than at the site that happens
    /// to write one.
    /// </summary>
    private sealed class Row
    {
        public Guid Pairing { get; set; }

        public Guid Item { get; set; }
    }
}
