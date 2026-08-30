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
/// one of the four bounds #37 names is here and three are not.
/// <see cref="ItemsPerRead"/> bounds a read that exists. How many resolutions
/// may be in flight is a property of a contract this plugin does not reference,
/// how many writes per unit of time wants a measurement against a real library,
/// and how long a pass may run has no pass to bound, so a number for any of the
/// three would be a setting an operator can move with nothing behind it.
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
}
