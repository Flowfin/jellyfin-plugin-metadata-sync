using System;
using System.Collections.ObjectModel;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
/// <remarks>
/// Everything an operator expresses about what this plugin does to their
/// library is here, and nothing else is. A property that is not read is a
/// surface an operator can set and the plugin ignores, so each one below is
/// carried by a validation rule that can refuse it.
/// <para>
/// What is deliberately absent. Nothing secret, and nothing that is a peer's
/// address: this file is serialised with the server's own permissions, read
/// back by the page, and pasted into a support thread by an operator whose
/// sync is not working. The schedule is not here yet either; it is the pass's
/// own setting and lands with it.
/// </para>
/// <para>
/// A bound lands with the thing it bounds rather than ahead of it, which is why
/// two of the four bounds #37 names are here and two are not.
/// <see cref="ItemsPerRead"/> bounds a read that exists and
/// <see cref="MinutesPerPass"/> bounds a pass that exists. How many
/// resolutions may be in flight is a property of a contract this plugin does
/// not reference, and how many writes per unit of time wants a measurement
/// against a real library, so a number for either would be a setting an
/// operator can move with nothing behind it.
/// </para>
/// <para>
/// The collections are read-only properties over a mutable collection rather
/// than settable arrays. That is the shape the server's serialiser fills in
/// place, and it means the property is never null for a validator to have to
/// think about.
/// </para>
/// <para>
/// One property is not a choice an operator makes. <see cref="Format"/> says
/// which shape this file is written in, and it is here rather than beside the
/// store because it describes this file: a configuration restored from a backup
/// carries its own shape with it, and a number kept anywhere else would describe
/// whichever of the two artefacts moved last. It is a stamp rather than a
/// setting, it is read and never chosen, and no page offers it.
/// </para>
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// How many items <see cref="ItemsPerRead"/> carries when an operator has
    /// not said otherwise.
    /// </summary>
    /// <remarks>
    /// It is the value the read has carried since it landed, and it is chosen
    /// rather than measured: big enough that a large library is not a hundred
    /// thousand round trips, small enough that one page is not itself worth
    /// bounding. #37 asks for a measured one, and the same measurement the write
    /// rate wants is what would produce it.
    /// <para>
    /// A configuration file written before this property existed carries no
    /// element for it, and the server's serialiser leaves a property no element
    /// names alone, so such a file reads back at this number rather than at
    /// zero. That is why the default is an initialiser here rather than a zero
    /// read as absent the way <see cref="Format"/>'s is: zero is a page size
    /// that never finishes a read, so it cannot also mean "not said".
    /// </para>
    /// </remarks>
    public const int ItemsPerReadDefault = 500;

    /// <summary>
    /// The largest page an operator may ask for.
    /// </summary>
    /// <remarks>
    /// The bound exists so that one page is not the library, and a setting that
    /// could be raised without limit would be a way to ask for the library one
    /// page at a time in a single page. Ten times the default is the room an
    /// operator has on a server with memory to spare, and it is still two orders
    /// of magnitude under the hundred thousand items the suite reads, so a read
    /// at the maximum is bounded in the sense the bound is about.
    /// <para>
    /// The number is a choice and not a measurement, in the same sense the
    /// default is one. What a measurement would move is both of them together.
    /// </para>
    /// </remarks>
    public const int ItemsPerReadMaximum = ItemsPerReadDefault * 10;

    /// <summary>
    /// How many minutes <see cref="MinutesPerPass"/> carries when an operator
    /// has not said otherwise.
    /// </summary>
    /// <remarks>
    /// An hour is chosen rather than measured, in the same sense
    /// <see cref="ItemsPerReadDefault"/> is. It is long enough that a first pass
    /// over a large library is not cut short on the run that has the most to do,
    /// and short enough that a pass started in a maintenance window is over
    /// before the window is. What a measurement against a real library would
    /// move is this number and the maximum below it together.
    /// <para>
    /// The unit is minutes rather than a duration because this is a number an
    /// operator types into a page. A pass that stops is not a pass that failed,
    /// so the number an operator gets wrong costs a pass that has to be run
    /// again rather than a library left half written.
    /// </para>
    /// </remarks>
    public const int MinutesPerPassDefault = 60;

    /// <summary>
    /// The longest pass an operator may ask for.
    /// </summary>
    /// <remarks>
    /// A day. The bound exists so that a pass ends, and a setting that could be
    /// raised without limit would be a way to ask for an unbounded pass one
    /// minute at a time. A pass still running when the next day's would start is
    /// not bounded in the sense the bound is about, so the day is where the
    /// range stops.
    /// <para>
    /// The number is a choice and not a measurement, exactly as
    /// <see cref="ItemsPerReadMaximum"/> is.
    /// </para>
    /// </remarks>
    public const int MinutesPerPassMaximum = MinutesPerPassDefault * 24;

    /// <summary>
    /// Gets or sets the pairing this configuration is for.
    /// </summary>
    /// <remarks>
    /// The empty value means no pairing has been chosen. That is a valid,
    /// inert state and not a defect: a plugin installed and not yet configured
    /// has nothing to sync and says nothing about it. It becomes a defect the
    /// moment the rest of the configuration expresses work, which is what the
    /// validator refuses.
    /// </remarks>
    public Guid PairingId { get; set; }

    /// <summary>
    /// Gets or sets which way metadata moves for this pairing.
    /// </summary>
    public SyncDirection Direction { get; set; }

    /// <summary>
    /// Gets the libraries that take part, by the identifier the server holds
    /// them under.
    /// </summary>
    /// <remarks>
    /// Participation is per library rather than one switch for the server, so
    /// an operator can pair a shared film library and leave a private one out.
    /// The empty set is valid and means no library takes part, rather than
    /// meaning all of them: a default that syncs everything is the one default
    /// nobody can undo after the first pass.
    /// </remarks>
    public Collection<Guid> ParticipatingLibraries { get; } = new();

    /// <summary>
    /// Gets the fields this operator does not want moved, out of the fields the
    /// register allows to move.
    /// </summary>
    /// <remarks>
    /// The override narrows and never widens. The register decides which fields
    /// may move at all and why, and a configuration that could add one back
    /// would be an operator overruling a decision argued in the register from a
    /// settings page. Naming a field the register already refuses is refused
    /// too, because it is a setting that does nothing and reads like one that
    /// does something.
    /// </remarks>
    public Collection<string> ExcludedFields { get; } = new();

    /// <summary>
    /// Gets or sets the shape this configuration is written in.
    /// </summary>
    /// <remarks>
    /// <see cref="ConfigurationFormat.Absent"/> is what the serialiser leaves
    /// for a file that carries no stamp, and it is read as
    /// <see cref="ConfigurationFormat.Earliest"/>. A stamp this build cannot
    /// place is refused by validation rather than read generously, which
    /// disables every action the same way any other unusable configuration does.
    /// <para>
    /// Nothing in this plugin writes it. What would is the migration step that
    /// changes the shape, and there has been one shape, so there is no step. The
    /// property exists before that because a shape change met by a build with no
    /// stamp to read is the case a stamp cannot be added after the fact for.
    /// </para>
    /// </remarks>
    public int Format { get; set; }

    /// <summary>
    /// Gets or sets how many items a pass asks the server for at once.
    /// </summary>
    /// <remarks>
    /// This is the bound on how much of a library is in memory while a pass
    /// reads one, and it is a setting rather than a constant because the number
    /// behind it is chosen rather than measured: an operator whose server is
    /// small, or is doing something else, is the person who finds out that 500
    /// is the wrong number for them, and a rebuild is not a repair they can
    /// make.
    /// <para>
    /// The range is one item up to <see cref="ItemsPerReadMaximum"/>, and both
    /// ends are refused rather than clamped. Zero is the end worth naming: a
    /// page of no items advances a read by nothing, so a read taking it would
    /// ask the server for nothing forever rather than answer with nothing.
    /// </para>
    /// </remarks>
    public int ItemsPerRead { get; set; } = ItemsPerReadDefault;

    /// <summary>
    /// Gets or sets how many minutes a pass may run before it stops itself.
    /// </summary>
    /// <remarks>
    /// This is the bound on how long a pass holds a server's attention, and it
    /// is a setting for the reason the page size is: the number behind it is
    /// chosen rather than measured, and the operator whose server is the one it
    /// is wrong for cannot make a rebuild.
    /// <para>
    /// A pass that reaches it stops at an item boundary and says it did not
    /// finish. It does not throw, and it keeps what it recorded, so the pass
    /// after it continues from where this one stopped rather than reading the
    /// library again. That is the whole difference between this bound and an
    /// operator cancelling a pass.
    /// </para>
    /// <para>
    /// The range is one minute up to <see cref="MinutesPerPassMaximum"/>, and
    /// both ends are refused rather than clamped, for the reason the page size's
    /// ends are. Zero is the end worth naming: a pass allowed no time stops
    /// before its first item on every run, so a library would never be written
    /// and every pass would report a success over nothing.
    /// </para>
    /// </remarks>
    public int MinutesPerPass { get; set; } = MinutesPerPassDefault;
}
