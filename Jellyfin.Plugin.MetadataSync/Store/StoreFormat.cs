using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

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

    /// <summary>
    /// The steps that take a store directory from one format to the next, in no
    /// particular order: which of them runs is decided by the format the
    /// directory declares rather than by a step's position in this list.
    /// </summary>
    /// <remarks>
    /// It is empty because one format has existed. That is the state #59 asks
    /// for this mechanism to be built in, and the emptiness is held rather than
    /// left to be noticed: <c>StoreMigrationTests</c> compares the formats this
    /// list steps from with every format below <see cref="Current"/>, so raising
    /// <see cref="Current"/> without writing the step reddens the suite instead
    /// of shipping a build that refuses every store the one before it wrote.
    /// </remarks>
    internal static readonly IReadOnlyList<FormatStep> Chain = Array.Empty<FormatStep>();

    /// <summary>
    /// What every migration in this process runs under, whichever directory it
    /// is over and whichever store asked for it.
    /// </summary>
    /// <remarks>
    /// Every store runs the migration from its own constructor before it reads
    /// the directory, and the stores open over one directory, so two of them
    /// opening at once would be two walks over one directory, each copying
    /// and moving what the other is in the middle of. The gate is one object
    /// for the process rather than one per directory: what that costs is that a
    /// migration over a directory nothing else is touching waits for a walk of
    /// length zero somewhere else, and what a gate per path would cost is a
    /// table to keep. It is private because the multithreading analyzer refuses
    /// a lock on a member anything else could take, and a proof asks whether a
    /// step is running under it through <see cref="MigrationGateIsHeld"/>
    /// rather than by reaching the object.
    /// </remarks>
    private static readonly object _migrationGate = new();

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _directory;
    private readonly string _path;
    private readonly int _current;
    private readonly IReadOnlyList<FormatStep> _chain;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreFormat"/> class over the
    /// directory this plugin keeps its own data in.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <exception cref="ArgumentNullException">There is no directory to read a stamp from.</exception>
    /// <exception cref="ArgumentException">The directory is named by nothing but space.</exception>
    public StoreFormat(string directory)
        : this(directory, Current, Chain)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreFormat"/> class reading
    /// and writing a format other than the one this build carries.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="current">The format to read and write.</param>
    /// <param name="chain">The steps that reach it.</param>
    /// <remarks>
    /// A seam for a proof and not an API for anybody else to call, in the sense
    /// `Properties/AssemblyInfo.cs` sets out. One format has existed, so a
    /// migration run against <see cref="Current"/> has nothing to do and every
    /// refusal inside <see cref="Migrate()"/> is unreachable from the public
    /// constructor. What this seam costs is one internal member; what it buys is
    /// a mechanism whose first execution is not on somebody's library.
    /// </remarks>
    internal StoreFormat(string directory, int current, IReadOnlyList<FormatStep> chain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentOutOfRangeException.ThrowIfLessThan(current, Earliest);

        _directory = directory;
        _path = Path.Combine(directory, FileName);
        _current = current;
        _chain = chain;
    }

    /// <summary>
    /// Gets a value indicating whether the thread asking holds the gate every
    /// migration in this process runs under.
    /// </summary>
    /// <remarks>
    /// A seam for a proof and not an API for anybody else to call, in the sense
    /// `Properties/AssemblyInfo.cs` sets out. A step asks it from inside the
    /// walk, which is the one place the answer says anything: the gate is
    /// meant to be held there and nowhere else.
    /// </remarks>
    internal static bool MigrationGateIsHeld => Monitor.IsEntered(_migrationGate);

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
            throw new StoreFormatRefusedException(_path, "no store format this build can read", _current);
        }

        if (stamp.Format > _current)
        {
            throw new StoreFormatRefusedException(_path, Says(stamp.Format), _current);
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

        Write(_path, _current);
    }

    /// <summary>
    /// Steps this directory forward to the format this build reads, one format
    /// at a time, and answers with how many steps ran.
    /// </summary>
    /// <returns>The number of steps applied, which is zero for a directory already in this format.</returns>
    /// <exception cref="StoreFormatRefusedException">The stamp says a format this build cannot place, or no step in the chain starts from a format the directory has to pass through.</exception>
    /// <remarks>
    /// The format is read first, so a directory this build cannot place is
    /// refused before anything is copied and the refusal writes nothing, which
    /// is the property <see cref="Declared()"/> already holds and this member
    /// does not weaken.
    /// <para>
    /// Every step runs over a copy beside the store, and the copy reaches the
    /// path readers open in one move at the end. A step that throws costs the
    /// copy and leaves the original directory exactly as the build that wrote it
    /// left it, which is the guarantee that makes a half-run migration a state
    /// this route cannot reach. The same reasoning is why
    /// <see cref="WrittenValues"/> compacts into a replacement and moves it: the
    /// move is the one step that has to be all or nothing.
    /// </para>
    /// <para>
    /// Every store runs this from its own constructor, before a line of its
    /// file is read, so the first step somebody writes runs on the first start
    /// after the upgrade with nothing wired for it. On every installation today
    /// the walk has length zero, because the chain is empty; the route is proved
    /// against a chain a fixture declares, through the seam each store carries
    /// for it. #59 is where the route is argued.
    /// </para>
    /// <para>
    /// Every walk in this process runs under one gate, so two stores opening at
    /// once over one directory walk it in turn rather than at once. What the
    /// gate does not hold off is a second process holding a file in either
    /// directory open, because the move is what fails then; no route here
    /// starts a migration while a pass is running, and that stays an
    /// arrangement rather than a lock.
    /// </para>
    /// </remarks>
    public int Migrate()
    {
        lock (_migrationGate)
        {
            return StepForward();
        }
    }

    /// <summary>
    /// The walk itself, run under the gate <see cref="Migrate()"/> takes.
    /// </summary>
    /// <returns>The number of steps applied.</returns>
    private int StepForward()
    {
        var declared = Declared();

        if (declared == _current)
        {
            return 0;
        }

        var steps = new List<FormatStep>();

        for (var format = declared; format < _current; format++)
        {
            var reaching = _chain.Where(step => step.From == format).ToList();

            if (reaching.Count != 1)
            {
                throw new StoreFormatRefusedException(_path, Unstepped(declared, format, reaching.Count), _current);
            }

            steps.Add(reaching[0]);
        }

        var working = _directory + ".migrating";
        var superseded = _directory + ".superseded";

        Discard(working);
        Discard(superseded);
        Copy(_directory, working);

        var stepped = false;

        try
        {
            foreach (var step in steps)
            {
                step.Apply(working);
            }

            Write(Path.Combine(working, FileName), _current);
            stepped = true;
        }
        finally
        {
            if (!stepped)
            {
                Discard(working);
            }
        }

        Directory.Move(_directory, superseded);
        Directory.Move(working, _directory);
        Discard(superseded);

        return steps.Count;
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
    /// Writes a stamp, replacing one that is there.
    /// </summary>
    /// <param name="path">The stamp file.</param>
    /// <param name="format">The format it declares.</param>
    private static void Write(string path, int format) =>
        File.WriteAllText(
            path,
            string.Format(CultureInfo.InvariantCulture, "{{\"format\":{0}}}\n", format),
            new UTF8Encoding(false));

    /// <summary>
    /// Copies a directory and everything under it.
    /// </summary>
    /// <param name="from">The directory to copy.</param>
    /// <param name="to">Where the copy goes.</param>
    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);

        foreach (var directory in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, directory)));
        }

        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), overwrite: true);
        }
    }

    /// <summary>
    /// Removes a directory this member made, where one is there.
    /// </summary>
    /// <param name="directory">The directory.</param>
    private static void Discard(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A gap in the chain, as the refusal says it.
    /// </summary>
    /// <param name="declared">The format the directory is in.</param>
    /// <param name="format">The format nothing steps from.</param>
    /// <param name="found">How many steps start from it.</param>
    /// <returns>The sentence.</returns>
    private static string Unstepped(int declared, int format, int found) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "store format {0}, and {1} of the steps that would carry it forward start from format {2}",
            declared,
            found,
            format);

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
