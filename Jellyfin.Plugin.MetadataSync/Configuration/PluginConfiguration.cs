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
/// sync is not working. The schedule and the bounds a pass runs under are not
/// here yet either; they are the pass's own settings and land with it.
/// </para>
/// <para>
/// The collections are read-only properties over a mutable collection rather
/// than settable arrays. That is the shape the server's serialiser fills in
/// place, and it means the property is never null for a validator to have to
/// think about.
/// </para>
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
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
}
