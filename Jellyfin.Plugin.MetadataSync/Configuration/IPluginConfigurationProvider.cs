using System.Collections.Generic;

namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// Supplies this plugin's configuration to the services that need it, and
/// refuses to supply one that cannot be acted on.
/// </summary>
/// <remarks>
/// Exists so that nothing but the plugin entry point has to know the
/// configuration comes from a static. A service that reads
/// <c>Plugin.Instance.Configuration</c> cannot be handed a different
/// configuration in a test, which is how a suite ends up mutating a global
/// between tests and how two tests that pass alone fail together.
/// <para>
/// It is also the moment the configuration is read back. The server
/// deserialises a file it does not know the rules for, so a file edited by
/// hand, or written by an older version of this plugin, arrives here as a
/// well-formed object holding values nothing may act on. Refusing at this seam
/// is what makes that a stop rather than a pass that syncs under rules nobody
/// chose, and it disables every action by construction: the only route to a
/// configuration is through this interface.
/// </para>
/// <para>
/// The two members answer different readers and neither caches. What is
/// refused is the configuration a caller would act on; the problems are handed
/// over plainly, because a page has to say what is wrong while every action is
/// refused.
/// </para>
/// </remarks>
public interface IPluginConfigurationProvider
{
    /// <summary>
    /// Returns the configuration to act on.
    /// </summary>
    /// <returns>The configuration.</returns>
    /// <exception cref="ConfigurationRefusedException">
    /// The configuration cannot be acted on. The exception carries every
    /// reason.
    /// </exception>
    PluginConfiguration Require();

    /// <summary>
    /// Returns every reason the configuration cannot be acted on, and refuses
    /// nothing. An empty answer means it can.
    /// </summary>
    /// <returns>The problems, or an empty list.</returns>
    IReadOnlyList<ConfigurationProblem> Problems();
}
