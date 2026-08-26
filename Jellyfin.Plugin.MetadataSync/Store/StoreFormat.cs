using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// The format the files in this plugin's store directory are written in, read
/// before any of them is opened.
/// </summary>
/// <remarks>
/// The stamp is one number for the directory rather than one per file. What
/// migrates is the store, which will hold the record of what was written, the
/// unmatched register and the conflict log, and a chain per file would be three
/// chains that can disagree about which step a directory has had. #59 argues for
/// the mechanism before there is anything to migrate, and this is the half of it
/// that has an artefact to stamp.
/// <para>
/// A directory with no stamp is the earliest format rather than an error. Two
/// things produce one - a plugin that has never written, and a directory written
/// before the stamp existed - and both are read the same way because both hold
/// files of format <see cref="Earliest"/>. Every later format is stamped, so an
/// absent stamp cannot mean anything else.
/// </para>
/// <para>
/// Nothing is written here on the way in. Reading a directory does not create
/// one, because a directory with no store in it is the state a plugin is
/// installed in, and <see cref="WrittenValues"/> keeps that property by stamping
/// at its first write rather than at construction.
/// </para>
/// <para>
/// This is a store of the shape every store here answers through, and it holds
/// nothing for any pairing. It is in the report an operator is shown for the
/// reason <see cref="IPairingStore"/> gives: a store left out of that report
/// tells an operator less than the truth in the direction that reassures, and a
/// source that persists and answers for no pairing is refused by the suite.
/// </para>
/// </remarks>
public sealed class StoreFormat : IPairingStore
{
    /// <summary>
    /// The format this build reads and writes.
    /// </summary>
    public const int Current = 1;

    /// <summary>
    /// The format a directory carrying no stamp is read as.
    /// </summary>
    /// <remarks>
    /// It is the same number as <see cref="Current"/> because one format has
    /// existed so far, and it is named separately because the two answer
    /// different questions and stop being the same number at the first
    /// migration. A reader who takes them for one constant will treat every
    /// unstamped directory as current on the day they differ, which is the
    /// migration silently skipped.
    /// </remarks>
    public const int Earliest = 1;

    /// <summary>
    /// The stamp inside the store directory.
    /// </summary>
    internal const string FileName = "store-format.json";

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _directory;
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreFormat"/> class over the
    /// directory this plugin keeps its own data in.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <exception cref="ArgumentNullException">There is no directory to read a stamp from.</exception>
    /// <exception cref="ArgumentException">The directory is named by nothing but space.</exception>
    public StoreFormat(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _directory = directory;
        _path = Path.Combine(directory, FileName);
    }

    /// <summary>
    /// Gets the stamp file this format is read from and written to.
    /// </summary>
    public string Location => _path;

    /// <inheritdoc />
    public string Held => "nothing about any pairing: the format the files in this plugin's store directory are written in";

    /// <summary>
    /// The format this directory declares, refusing one this build cannot place.
    /// </summary>
    /// <returns>The format, which is <see cref="Earliest"/> where there is no stamp.</returns>
    /// <exception cref="StoreFormatRefusedException">The stamp says a format from the future, or says nothing this build can read.</exception>
    /// <remarks>
    /// The file is read on every call rather than kept. It is one short line, it
    /// is read a handful of times in a run, and a cached answer is one that goes
    /// on being given after the directory it describes has been replaced
    /// underneath it.
    /// </remarks>
    public int Declared()
    {
        if (!File.Exists(_path))
        {
            return Earliest;
        }

        Declaration? stamp;

        try
        {
            stamp = JsonSerializer.Deserialize<Declaration>(File.ReadAllText(_path), _json);
        }
        catch (JsonException)
        {
            stamp = null;
        }

        if (stamp is null || stamp.Format < Earliest)
        {
            // Refused rather than read as the earliest format. Reading it as the
            // earliest is the assumption that fails in the destroying direction:
            // a newer file whose stamp was damaged would be opened, dropped to
            // what this build understands, and written back.
            throw new StoreFormatRefusedException(_path, "no store format this build can read", Current);
        }

        if (stamp.Format > Current)
        {
            throw new StoreFormatRefusedException(_path, Says(stamp.Format), Current);
        }

        return stamp.Format;
    }

    /// <summary>
    /// Writes the stamp where there is none, so a directory this plugin has
    /// written to says which format it is in.
    /// </summary>
    /// <exception cref="StoreFormatRefusedException">The stamp already there says a format this build does not read.</exception>
    /// <remarks>
    /// The format is read first, so a directory that refused to open cannot be
    /// stamped over by the same run. A stamp already naming a format this build
    /// reads is left exactly as it is: rewriting it would move the file's
    /// timestamp on every pass and say nothing new.
    /// </remarks>
    public void Stamp()
    {
        if (Declared() != Earliest || File.Exists(_path))
        {
            return;
        }

        Directory.CreateDirectory(_directory);

        File.WriteAllText(
            _path,
            string.Format(CultureInfo.InvariantCulture, "{{\"format\":{0}}}\n", Current),
            new UTF8Encoding(false));
    }

    /// <inheritdoc />
    /// <remarks>
    /// No rows, on every pairing. The format a directory is written in is not
    /// data about anybody's relationship with another server, and answering with
    /// a row would put a number in an operator's report that no removal can take
    /// away.
    /// </remarks>
    public PairingHolding Holding(Guid pairingId) => new()
    {
        Store = nameof(StoreFormat),
        Held = Held,
        Rows = Array.Empty<string>(),
    };

    /// <inheritdoc />
    /// <remarks>
    /// Nothing goes. The stamp says how to read what is left after a removal, so
    /// a removal that deleted it would leave the next run reading a directory
    /// that no longer says what it is.
    /// </remarks>
    public int Remove(Guid pairingId) => 0;

    /// <summary>
    /// A format, as the refusal says it.
    /// </summary>
    /// <param name="format">The format the stamp declares.</param>
    /// <returns>The sentence.</returns>
    private static string Says(int format) =>
        string.Format(CultureInfo.InvariantCulture, "store format {0}, which is newer than this build", format);

    /// <summary>
    /// What the stamp file carries. It is a type of its own so the member name on
    /// the disk is decided here rather than at the site that happens to write
    /// one.
    /// </summary>
    private sealed class Declaration
    {
        public int Format { get; set; }
    }
}
