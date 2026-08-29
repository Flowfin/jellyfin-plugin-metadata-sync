using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Conflicts;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// The plugin's own account of what it decided, kept in a file of its own
/// beside the plugin's data.
/// </summary>
/// <remarks>
/// It is the third store and the reason it is not one of the other two is the
/// key and the lifetime. What this plugin wrote is filed per item and per field
/// and is true until the value is overwritten; how far a pass got is true of one
/// pass and is thrown away when it ends. A decision is true of the moment it was
/// taken and is kept until the bound pushes it out, and it includes the
/// decisions that wrote nothing, which is most of what an operator opens the log
/// to read.
/// <para>
/// The file is a line per decision rather than one document rewritten, and it
/// fails the same way <see cref="WrittenValues"/> does: a line either reaches
/// the disk whole or it does not, so a pass killed part way through leaves a
/// file whose last line may be short and whose earlier lines are intact. A short
/// line is dropped on the next read and counted in <see cref="Unreadable"/>
/// rather than throwing, because a log that refuses to open after a power cut is
/// a log an operator meets at exactly the moment they need it.
/// </para>
/// <para>
/// A line carries the values as a row shows them rather than as the library
/// holds them, which is <see cref="ShownValue"/>'s cut and its flag. Keeping the
/// whole of both sides would put two overviews per decision on this disk for a
/// page that shows neither of them whole, and the cut is already the shape the
/// account is written in.
/// </para>
/// <para>
/// What this type does not do. It decides nothing: which rows are owed is
/// <see cref="ConflictEntries"/>' answer and this keeps what it is handed. It
/// shows nothing, groups nothing and exports nothing, which are the rest of #48
/// and need a surface this plugin has not got.
/// </para>
/// </remarks>
public sealed class ConflictLog : IPairingStore
{
    /// <summary>
    /// How many decisions are kept per pairing, oldest dropped first.
    /// </summary>
    /// <remarks>
    /// A count rather than an age, for the reason the other bound is one:
    /// nothing here compares two servers' clocks and an age would need one.
    /// <para>
    /// Per pairing rather than in total, so an operator who pairs with two
    /// households does not lose one relationship's account to the other's first
    /// pass. It is not the ten-per-item-and-field bound the written-value store
    /// carries, and the difference is the point rather than a detail: a log
    /// bounded per field would drop the refusals an operator opens it to find,
    /// because a field refused every pass would push its own history out.
    /// </para>
    /// <para>
    /// Five thousand is a choice and this is the argument for it. A first pass
    /// over a modest library decides tens of thousands of fields, so keeping
    /// everything makes the log a second copy of the library. Five thousand
    /// holds a whole pass over a few hundred items, which is the case an
    /// operator is reading after, and it bounds the file at a few megabytes per
    /// pairing rather than at the size of a library.
    /// </para>
    /// </remarks>
    public const int Bound = 5000;

    /// <summary>
    /// The file inside the store directory. One file rather than one per
    /// pairing, because a pairing is a component of every key and a file per
    /// pairing would answer the same questions with more places to be wrong.
    /// </summary>
    internal const string FileName = "conflict-log.jsonl";

    /// <summary>
    /// How many superseded lines a file may carry before it is rewritten.
    /// </summary>
    /// <remarks>
    /// A superseded line here is one the bound has pushed out. The store that
    /// keeps written values carries the same floor for the same trade: a file
    /// rewritten on every append pays the whole-file cost the append exists to
    /// avoid, and one never rewritten grows without end on a pairing that keeps
    /// deciding.
    /// </remarks>
    private const int CompactionFloor = 512;

    private static readonly JsonSerializerOptions _json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _gate = new();

    /// <summary>
    /// What is held, filed under the pairing alone, newest last.
    /// </summary>
    /// <remarks>
    /// The pairing is the whole of the key because the bound is per pairing and
    /// because a revocation is terminal, so one pairing's account cannot be read
    /// as the account of the pairing that replaced it. What a row is about
    /// beneath that is a column rather than a key: an operator reads this log by
    /// what happened and when, not by looking one item up.
    /// </remarks>
    private readonly Dictionary<Guid, List<ConflictEntry>> _held = new();

    /// <summary>
    /// The highest position each pairing's log has reached, which is what says
    /// how many entries the bound has discarded without the discarded entries
    /// being kept to be counted.
    /// </summary>
    private readonly Dictionary<Guid, long> _reached = new();

    private readonly StoreFormat _format;
    private readonly string _directory;
    private readonly string _path;
    private int _lines;
    private bool _stamped;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictLog"/> class over a
    /// directory, reading back whatever an earlier run left there.
    /// </summary>
    /// <param name="directory">The directory this plugin keeps its own data in.</param>
    /// <exception cref="ArgumentException">The directory is named by nothing but space.</exception>
    /// <remarks>
    /// Reading in the constructor is what makes a restart invisible to a caller,
    /// and on this store it is what makes the account survive one at all. A
    /// directory with no file in it produces an instance holding nothing rather
    /// than an empty file.
    /// </remarks>
    public ConflictLog(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _directory = directory;
        _path = Path.Combine(directory, FileName);

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
    /// Counted rather than swallowed, for the reason the other store counts its
    /// own. Each one is a decision this plugin took and can no longer show, and
    /// a surface reporting a clean count while this number is above zero is
    /// telling an operator the account is complete when it is not.
    /// </remarks>
    public int Unreadable { get; private set; }

    /// <inheritdoc />
    public string Held => "the decisions this plugin took about fields on this server's library, and what it did about each";

    /// <summary>
    /// Keeps one decision.
    /// </summary>
    /// <param name="pairingId">The pairing the decision was taken under.</param>
    /// <param name="entry">The decision.</param>
    /// <exception cref="ArgumentNullException">There is no entry to keep.</exception>
    /// <remarks>
    /// The bound is applied on the way in rather than on the way out, so the
    /// answer a reader gets and the answer a rewrite writes are the same list
    /// and cannot drift apart.
    /// </remarks>
    public void Record(Guid pairingId, ConflictEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            if (!_held.TryGetValue(pairingId, out var entries))
            {
                entries = new List<ConflictEntry>();
                _held[pairingId] = entries;
            }

            var reached = _reached.TryGetValue(pairingId, out var last) ? last + 1 : 1;
            _reached[pairingId] = reached;

            entries.Add(entry);

            while (entries.Count > Bound)
            {
                entries.RemoveAt(0);
            }

            Append(Line(pairingId, reached, entry));
        }
    }

    /// <summary>
    /// What is kept for one pairing, oldest first.
    /// </summary>
    /// <param name="pairingId">The pairing to ask about.</param>
    /// <returns>The decisions still held, which is at most <see cref="Bound"/>.</returns>
    public IReadOnlyList<ConflictEntry> Entries(Guid pairingId)
    {
        lock (_gate)
        {
            return _held.TryGetValue(pairingId, out var entries)
                ? new ReadOnlyCollection<ConflictEntry>(entries.ToList())
                : Array.Empty<ConflictEntry>();
        }
    }

    /// <summary>
    /// How many decisions the bound has pushed out of one pairing's account.
    /// </summary>
    /// <param name="pairingId">The pairing to ask about.</param>
    /// <returns>The number discarded, which is zero on an account inside the bound.</returns>
    /// <remarks>
    /// Derived from how far the log has got rather than from a tally somebody
    /// kept, which is what makes it survive a restart and a rewrite of the file.
    /// A tally held beside the entries would be reset by the rewrite that drops
    /// the superseded lines, and the number an operator would then be shown is a
    /// clean one - which is the failure this issue and #66 both name, arriving
    /// through the repair rather than through the bound.
    /// <para>
    /// It is not a claim about what was in the entries that went. This store
    /// knows how many it dropped and nothing about them, which is what a bound
    /// costs and is why the surface has to say the account is incomplete rather
    /// than reporting what is left as though it were all of it.
    /// </para>
    /// </remarks>
    public int Dropped(Guid pairingId)
    {
        lock (_gate)
        {
            var reached = _reached.TryGetValue(pairingId, out var last) ? last : 0;
            var held = _held.TryGetValue(pairingId, out var entries) ? entries.Count : 0;

            return (int)(reached - held);
        }
    }

    /// <inheritdoc />
    public PairingHolding Holding(Guid pairingId)
    {
        lock (_gate)
        {
            return new PairingHolding
            {
                Store = nameof(ConflictLog),
                Held = Held,
                Rows = _held.TryGetValue(pairingId, out var entries)
                    ? entries.Select(Sentence).ToList()
                    : new List<string>(),
            };
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The file is rewritten from what is left rather than having lines struck
    /// out of it, and how far each remaining pairing had got is rewritten with
    /// it. A removal that forgot the second would answer correctly until the
    /// next restart and then report every other pairing's account as complete.
    /// </remarks>
    public int Remove(Guid pairingId)
    {
        lock (_gate)
        {
            if (!_held.TryGetValue(pairingId, out var entries))
            {
                _reached.Remove(pairingId);
                return 0;
            }

            var removed = entries.Count;

            _held.Remove(pairingId);
            _reached.Remove(pairingId);

            Compact(_held.Values.Sum(held => held.Count));

            return removed;
        }
    }

    /// <summary>
    /// Says what the store holds, for a message an operator reads.
    /// </summary>
    /// <returns>A sentence naming the file and what is in it.</returns>
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0} holds {1} decision(s) across {2} pairing(s), at most {3} each.",
        _path,
        _held.Values.Sum(entries => entries.Count),
        _held.Count,
        Bound);

    /// <summary>
    /// One decision, said in a sentence.
    /// </summary>
    /// <param name="entry">The decision.</param>
    /// <returns>The sentence.</returns>
    /// <remarks>
    /// A value that was cut is said to have been cut. A sentence showing the
    /// first two hundred characters of an overview as though they were the whole
    /// of it is the failure the row's own flag exists against, and a report an
    /// operator reads is where it would do the most damage.
    /// </remarks>
    private static string Sentence(ConflictEntry entry)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} on item {1}: here {2}, on the peer {3}, {4} by {5}, at {6}",
            entry.Field,
            entry.Item,
            Said(entry.LocalValue),
            Said(entry.PeerValue),
            entry.Outcome,
            entry.Rule ?? "no declared rule",
            entry.At.ToString("u", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A value as the sentence says it.
    /// </summary>
    /// <param name="value">The value as the row shows it.</param>
    /// <returns>The value in quotation marks, said to be cut where it was.</returns>
    private static string Said(ShownValue value) => value.Text is null
        ? "nothing"
        : string.Format(
            CultureInfo.InvariantCulture,
            value.Truncated ? "\"{0}\" (cut)" : "\"{0}\"",
            value.Text);

    private static Row Line(Guid pairingId, long reached, ConflictEntry entry) => new()
    {
        Pairing = pairingId,
        Reached = reached,
        Item = entry.Item,
        Field = entry.Field,
        Local = entry.LocalValue.Text,
        LocalCut = entry.LocalValue.Truncated,
        Peer = entry.PeerValue.Text,
        PeerCut = entry.PeerValue.Truncated,
        Rule = entry.Rule,
        Outcome = entry.Outcome,
        Direction = entry.Direction,
        At = entry.At,
    };

    private static ConflictEntry Entry(Row row) => new()
    {
        Item = row.Item,
        Field = row.Field,
        LocalValue = new ShownValue { Text = row.Local, Truncated = row.LocalCut },
        PeerValue = new ShownValue { Text = row.Peer, Truncated = row.PeerCut },
        Rule = row.Rule,
        Outcome = row.Outcome,
        Direction = row.Direction,
        At = row.At,
    };

    /// <summary>
    /// Reads the file back, applying the bound as it goes.
    /// </summary>
    /// <remarks>
    /// The lines are replayed in the order they were written, so a pairing whose
    /// lines exceed the bound keeps the last <see cref="Bound"/> of them. How
    /// far each pairing had got is taken from the highest position its lines
    /// carry rather than from their count, which is the whole reason the
    /// position is on the disk: the lines the bound dropped are not there to be
    /// counted.
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

            if (row is null || string.IsNullOrWhiteSpace(row.Field) || row.Reached <= 0)
            {
                Unreadable++;
                continue;
            }

            if (!_held.TryGetValue(row.Pairing, out var entries))
            {
                entries = new List<ConflictEntry>();
                _held[row.Pairing] = entries;
            }

            entries.Add(Entry(row));

            if (!_reached.TryGetValue(row.Pairing, out var reached) || row.Reached > reached)
            {
                _reached[row.Pairing] = row.Reached;
            }

            while (entries.Count > Bound)
            {
                entries.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Adds one line to the file, and rewrites the file when it is carrying
    /// enough superseded lines to be worth the cost.
    /// </summary>
    private void Append(Row row)
    {
        Directory.CreateDirectory(_directory);

        // Stamped at the first write rather than at construction, for the reason
        // written at the same place in the store that keeps written values: a
        // plugin that has been installed and has decided nothing leaves no file.
        if (!_stamped)
        {
            _format.Stamp();
            _stamped = true;
        }

        File.AppendAllText(_path, JsonSerializer.Serialize(row, _json) + "\n", new UTF8Encoding(false));
        _lines++;

        var retained = _held.Values.Sum(entries => entries.Count);

        if (_lines - retained >= CompactionFloor)
        {
            Compact(retained);
        }
    }

    /// <summary>
    /// Writes what is held to a new file and moves it over the old one.
    /// </summary>
    /// <remarks>
    /// Written beside the store and moved onto it, so a rewrite interrupted half
    /// way leaves the file it was replacing untouched.
    /// <para>
    /// The positions are rewritten as they were rather than renumbered from one.
    /// Renumbering would make every pairing's account read as complete the next
    /// time it was opened, which is the one thing this file is carrying the
    /// position for.
    /// </para>
    /// </remarks>
    private void Compact(int retained)
    {
        var replacement = _path + ".compacting";

        var lines = _held
            .SelectMany(pairing => Positioned(pairing.Key, pairing.Value))
            .Select(row => JsonSerializer.Serialize(row, _json));

        File.WriteAllText(replacement, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        File.Move(replacement, _path, overwrite: true);
        _lines = retained;
    }

    /// <summary>
    /// One pairing's held entries, with the positions they occupy in a log that
    /// has reached where this one has.
    /// </summary>
    /// <remarks>
    /// The last entry sits at the position the log has reached and the ones
    /// before it count back from there, which is what they were when they were
    /// written: the bound drops from the front, so the entries that are left are
    /// the newest and the newest is the highest.
    /// </remarks>
    private IEnumerable<Row> Positioned(Guid pairingId, List<ConflictEntry> entries)
    {
        var reached = _reached.TryGetValue(pairingId, out var last) ? last : entries.Count;
        var first = reached - entries.Count + 1;

        return entries.Select((entry, offset) => Line(pairingId, first + offset, entry));
    }

    /// <summary>
    /// What one line of the file carries. It is a type of its own so the member
    /// names on the disk are decided here rather than at the site that happens to
    /// write one.
    /// </summary>
    /// <remarks>
    /// The two named answers are written as their names rather than as their
    /// numbers. A member added to either would move the numbers under a file an
    /// earlier build wrote, and one of the two is a type this plan expects to
    /// gain a member.
    /// </remarks>
    private sealed class Row
    {
        public Guid Pairing { get; set; }

        public long Reached { get; set; }

        public Guid Item { get; set; }

        public string Field { get; set; } = string.Empty;

        public string? Local { get; set; }

        public bool LocalCut { get; set; }

        public string? Peer { get; set; }

        public bool PeerCut { get; set; }

        public string? Rule { get; set; }

        public ConflictOutcome Outcome { get; set; }

        public SyncDirection Direction { get; set; }

        public DateTimeOffset At { get; set; }
    }
}
