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
/// The plugin's own store of what it wrote, kept in a file of its own beside
/// the plugin's data and never in the plugin configuration.
/// </summary>
/// <remarks>
/// The split is the one <c>docs/storage.md</c> argues. The configuration holds
/// choices an operator made and is the file they attach to a bug report; this
/// holds data that grows with the library and is nobody's setting. Putting the
/// record in the configuration would have been the one place a plugin can
/// persist without designing anything, and it would have made that sentence in
/// the operator guide false.
/// <para>
/// The file is a line per write rather than one document rewritten. A first
/// pass over a modest library writes tens of thousands of fields, and a store
/// that rewrote the whole file per field would cost the square of that. An
/// append also fails cleanly: a line either reaches the disk whole or it does
/// not, so a pass killed part way through leaves a file whose last line may be
/// short and whose earlier lines are all intact. A short last line is dropped
/// on the next read and counted in <see cref="Unreadable"/> rather than
/// throwing, because a store that refuses to open after a power cut is a store
/// that has turned one lost write into every lost write.
/// </para>
/// <para>
/// The bound is <see cref="Bound"/> values per item and per field, oldest
/// dropped first, and it is a count rather than an age because nothing here
/// compares two servers' clocks. It is applied on the way in, so what the file
/// holds after a compaction is what the bound allows and not more. What a bound
/// discarded is gone: #66 is where a surface reporting on attribution has to say
/// that rather than report a clean number.
/// </para>
/// <para>
/// A line carries the value written and the value that was there before it,
/// which is one line rather than two because they are one write. The previous
/// value arrives from the caller and is never worked out here: what this store
/// holds from the last pass is what this plugin wrote, and what a write replaces
/// is what the library held at the moment of the write. The two differ by
/// exactly the edit an operator made in between, which is the one thing a
/// conflict log entry and a revert both need. A null on either member is a field
/// that held nothing rather than a member nobody recorded, and the empty history
/// is what says there is no record at all.
/// </para>
/// <para>
/// What this type does not do. It does not decide anything - which value wins is
/// the conflict rules' answer and this is one of their inputs. It does not know
/// what a field means, so it never asks the register. And it holds nothing about
/// a peer beyond the pairing identifier the contract will hand over, which is
/// the row <c>docs/storage.md</c> gives to neither place.
/// </para>
/// </remarks>
public sealed class WrittenValues : IWrittenValues, IPairingStore
{
    /// <summary>
    /// How many values are kept per item and per field, oldest dropped first.
    /// </summary>
    /// <remarks>
    /// Decided on #16 on 2026-08-24. Ten is more than the rules need today,
    /// which decide from the newest value alone, and it is what gives #64 a
    /// revert with something to revert to. It is stated as a number rather than
    /// as a size because a number is what an operator can be told, and it bounds
    /// the store at ten values per field the register lets move rather than at
    /// the history of a library.
    /// </remarks>
    public const int Bound = 10;

    /// <summary>
    /// The file inside the store directory. It is one file rather than one per
    /// pairing, because a pairing is a component of every key and a store split
    /// by it would answer the same questions with more places to be wrong.
    /// </summary>
    internal const string FileName = "written-values.jsonl";

    /// <summary>
    /// How many lines a file may carry beyond what its retained values need
    /// before it is rewritten, and the floor under which it is not rewritten at
    /// all.
    /// </summary>
    /// <remarks>
    /// A store that compacted on every append would pay the whole-file cost the
    /// append exists to avoid; one that never compacted would grow without end
    /// on a library that syncs the same fields every pass. What the file is
    /// carrying beyond what its values need is the superseded lines, and this is
    /// how many of them are tolerated before the file is rewritten.
    /// </remarks>
    private const int CompactionFloor = 512;

    private static readonly JsonSerializerOptions _json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();

    /// <summary>
    /// What is held, filed under the pairing, the item and the field.
    /// </summary>
    /// <remarks>
    /// The pairing is a component of the key because a revocation ends a pairing
    /// permanently and the identifier that replaces it is a different one, so
    /// rows of an ended pairing cannot be read as rows of the pairing that
    /// followed it, and one pairing's rows can be found by the key alone.
    /// <para>
    /// It is a tuple rather than a type of its own. A record struct would carry
    /// an equality, a printer and a deconstructor that nothing here calls, and
    /// the decision-code bar reads a line nothing calls as a line nothing
    /// exercises, which is the reading it should make.
    /// </para>
    /// </remarks>
    private readonly Dictionary<(Guid Pairing, Guid Item, string Field), List<WrittenValue>> _held = new();
    private readonly StoreFormat _format;
    private readonly string _directory;
    private readonly string _path;
    private int _lines;
    private bool _stamped;

    /// <summary>
    /// Initializes a new instance of the <see cref="WrittenValues"/> class over
    /// a directory, reading back whatever an earlier run left there.
    /// </summary>
    /// <param name="directory">The directory this plugin keeps its own data in.</param>
    /// <exception cref="ArgumentNullException">There is no directory to keep the store in.</exception>
    /// <exception cref="ArgumentException">The directory is named by nothing but space.</exception>
    /// <remarks>
    /// Reading in the constructor is what makes a restart invisible to a caller:
    /// an instance built over a directory an earlier instance wrote answers the
    /// same questions it did. Nothing is created here. A directory with no store
    /// in it is the state a plugin is installed in, and it produces an instance
    /// holding nothing rather than an empty file.
    /// </remarks>
    public WrittenValues(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _directory = directory;
        _path = Path.Combine(directory, FileName);

        // The format the directory declares is read before a line of it is,
        // because a file written by a newer build is dropped to what this build
        // understands the moment it is loaded and written back that way at the
        // next compaction. Refusing here is the only place that loss can still
        // be prevented rather than reported.
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
    /// Counted rather than swallowed. A line that did not reach the disk whole
    /// is an ordinary consequence of a pass being killed, and a line that is
    /// unreadable for any other reason is a defect; this store cannot tell the
    /// two apart and reports the count instead of choosing. A caller reporting
    /// attribution to an operator has to say a number above zero out loud,
    /// because each one is a value this plugin wrote and can no longer prove it
    /// wrote.
    /// </remarks>
    public int Unreadable { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Written for somebody reading a list of stores rather than reading this
    /// file, so it says what the rows are about instead of naming the type.
    /// </remarks>
    public string Held => "what this plugin wrote to this server's library, with the value each write replaced";

    /// <inheritdoc />
    public void Record(Guid pairingId, Guid itemId, string field, string? value, string? previousValue)
    {
        lock (_gate)
        {
            var key = (pairingId, itemId, Named(field));

            if (!_held.TryGetValue(key, out var values))
            {
                values = new List<WrittenValue>();
                _held[key] = values;
            }

            values.Add(new WrittenValue { Value = value, Previous = previousValue });

            // Trimmed on the way in rather than on the way out, so the answer a
            // reader gets and the answer a compaction writes are the same list
            // and cannot drift apart.
            while (values.Count > Bound)
            {
                values.RemoveAt(0);
            }

            Append(new Row
            {
                Pairing = pairingId,
                Item = itemId,
                Field = field,
                Value = value,
                Previous = previousValue,
            });
        }
    }

    /// <inheritdoc />
    public string? LastWritten(Guid pairingId, Guid itemId, string field)
    {
        lock (_gate)
        {
            return _held.TryGetValue((pairingId, itemId, Named(field)), out var values)
                ? values[^1].Value
                : null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<WrittenValue> History(Guid pairingId, Guid itemId, string field)
    {
        lock (_gate)
        {
            return _held.TryGetValue((pairingId, itemId, Named(field)), out var values)
                ? new ReadOnlyCollection<WrittenValue>(values.ToList())
                : Array.Empty<WrittenValue>();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<WrittenField> Fields(Guid pairingId)
    {
        lock (_gate)
        {
            return new ReadOnlyCollection<WrittenField>(_held.Keys
                .Where(key => key.Pairing == pairingId)
                .OrderBy(key => key.Item)
                .ThenBy(key => key.Field, StringComparer.Ordinal)
                .Select(key => new WrittenField { Item = key.Item, Field = key.Field })
                .ToList());
        }
    }

    /// <inheritdoc />
    public PairingHolding Holding(Guid pairingId)
    {
        lock (_gate)
        {
            return new PairingHolding
            {
                Store = nameof(WrittenValues),
                Held = Held,
                Rows = _held
                    .Where(entry => entry.Key.Pairing == pairingId)
                    .OrderBy(entry => entry.Key.Item)
                    .ThenBy(entry => entry.Key.Field, StringComparer.Ordinal)
                    .SelectMany(entry => entry.Value.Select(written => Sentence(entry.Key.Item, entry.Key.Field, written)))
                    .ToList(),
            };
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The file is rewritten from what is left rather than having lines struck
    /// out of it. A store that removed a pairing from memory and appended
    /// nothing would answer correctly until the next restart and then read every
    /// removed row back off the disk, which is the shape of a deletion that
    /// looks like it worked.
    /// </remarks>
    public int Remove(Guid pairingId)
    {
        lock (_gate)
        {
            var going = _held.Keys.Where(key => key.Pairing == pairingId).ToList();
            var removed = 0;

            foreach (var key in going)
            {
                removed += _held[key].Count;
                _held.Remove(key);
            }

            if (removed > 0)
            {
                Compact(_held.Values.Sum(values => values.Count));
            }

            return removed;
        }
    }

    /// <summary>
    /// One row of this store, said in a sentence.
    /// </summary>
    /// <param name="item">The item on this server.</param>
    /// <param name="field">The field, as the register names it.</param>
    /// <param name="written">What was written and what it replaced.</param>
    /// <returns>The sentence.</returns>
    /// <remarks>
    /// A value that was empty is said as empty rather than left out. A row
    /// reading as though a field held nothing when it held an empty string is a
    /// distinction an operator cannot recover from the document afterwards.
    /// </remarks>
    private static string Sentence(Guid item, string field, WrittenValue written)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} on item {1}: wrote {2}, replacing {3}",
            field,
            item,
            Said(written.Value),
            Said(written.Previous));
    }

    /// <summary>
    /// A value as the document says it.
    /// </summary>
    /// <param name="value">The value, or null where the field held nothing.</param>
    /// <returns>The value in quotation marks, or the word for an absence.</returns>
    private static string Said(string? value) =>
        value is null ? "nothing" : string.Format(CultureInfo.InvariantCulture, "\"{0}\"", value);

    /// <summary>
    /// The field a caller named, refused where it has no name.
    /// </summary>
    /// <remarks>
    /// One refusal rather than one per member. A store that filed a value under
    /// an empty name would answer a later question about a real field with it,
    /// and three copies of the same guard would be three sites the register
    /// cannot tell apart, because it keys a site by the line it is on.
    /// </remarks>
    /// <param name="field">The field a caller named.</param>
    /// <returns>The field, unchanged.</returns>
    private static string Named(string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        return field;
    }

    /// <summary>
    /// Reads the file back, applying the bound as it goes.
    /// </summary>
    /// <remarks>
    /// The lines are replayed in the order they were written, so the newest
    /// value for a key is the last line naming it, and a key whose lines exceed
    /// the bound keeps the last <see cref="Bound"/> of them. A line that cannot
    /// be read is skipped and counted: it is at the end of the file after a
    /// pass was killed, and anywhere else it is a defect this type cannot
    /// distinguish from that.
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

            if (row is null || string.IsNullOrWhiteSpace(row.Field))
            {
                Unreadable++;
                continue;
            }

            var key = (row.Pairing, row.Item, row.Field);

            if (!_held.TryGetValue(key, out var values))
            {
                values = new List<WrittenValue>();
                _held[key] = values;
            }

            values.Add(new WrittenValue { Value = row.Value, Previous = row.Previous });

            while (values.Count > Bound)
            {
                values.RemoveAt(0);
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

        // Stamped at the first write rather than at construction, so a plugin
        // that has been installed and has written nothing still leaves an empty
        // directory rather than a file. Once per instance: the stamp does not
        // change under a running plugin, and a check per appended line would be
        // one file system call per written field.
        if (!_stamped)
        {
            _format.Stamp();
            _stamped = true;
        }

        File.AppendAllText(_path, JsonSerializer.Serialize(row, _json) + "\n", new UTF8Encoding(false));
        _lines++;

        var retained = _held.Values.Sum(values => values.Count);

        if (_lines - retained >= CompactionFloor)
        {
            Compact(retained);
        }
    }

    /// <summary>
    /// Writes what is held to a new file and moves it over the old one.
    /// </summary>
    /// <remarks>
    /// Written beside the store and moved onto it, so a compaction interrupted
    /// half way leaves the file it was replacing untouched. The move is the one
    /// step that has to be all or nothing, and it is the only step that touches
    /// the file a reader opens.
    /// </remarks>
    private void Compact(int retained)
    {
        var replacement = _path + ".compacting";

        var lines = _held
            .SelectMany(entry => entry.Value.Select(written => new Row
            {
                Pairing = entry.Key.Pairing,
                Item = entry.Key.Item,
                Field = entry.Key.Field,
                Value = written.Value,
                Previous = written.Previous,
            }))
            .Select(row => JsonSerializer.Serialize(row, _json));

        File.WriteAllText(replacement, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
        File.Move(replacement, _path, overwrite: true);
        _lines = retained;
    }

    /// <summary>
    /// Says what the store holds, for a message an operator reads.
    /// </summary>
    /// <returns>A sentence naming the file and what is in it.</returns>
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0} holds {1} value(s) across {2} item-and-field key(s), at most {3} each.",
        _path,
        _held.Values.Sum(values => values.Count),
        _held.Count,
        Bound);

    /// <summary>
    /// What one line of the file carries. It is a type of its own so the
    /// member names on the disk are decided here rather than at the site that
    /// happens to write one.
    /// </summary>
    private sealed class Row
    {
        public Guid Pairing { get; set; }

        public Guid Item { get; set; }

        public string Field { get; set; } = string.Empty;

        public string? Value { get; set; }

        public string? Previous { get; set; }
    }
}
