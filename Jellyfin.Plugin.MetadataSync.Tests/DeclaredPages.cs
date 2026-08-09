using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Asks the plugin which pages it hands the server, rather than working the
/// answer out a second time.
/// </summary>
/// <remarks>
/// The page path was written twice: once in the plugin, where the server reads
/// it, and once in each test that wanted to open the page. Both copies were the
/// same expression over the same namespace, so both moved together when the
/// namespace moved, which is the case the guards were written for. Neither moved
/// when the declaration itself changed, which is the case they were not: a page
/// declared at a path the assembly does not carry leaves every test green and an
/// operator with a configuration page that fails to load.
///
/// So this asks the plugin. The instance is never initialised, because
/// constructing the plugin properly means giving it the server's application
/// paths and a serialiser and letting it read a configuration file off a disk,
/// and none of that has anything to do with the question. What the page
/// declaration reads is the plugin's own name and its type's namespace, neither
/// of which is instance state.
/// </remarks>
internal static class DeclaredPages
{
    /// <summary>
    /// The pages the plugin declares to the server.
    /// </summary>
    /// <returns>The declarations, in the order the plugin hands them over.</returns>
    public static IReadOnlyList<PluginPageInfo> All()
    {
        var plugin = (IHasWebPages)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));

        return plugin.GetPages().ToList();
    }

    /// <summary>
    /// The embedded resource path of the one page the plugin declares.
    /// </summary>
    /// <returns>The path the server is told to serve the page from.</returns>
    public static string ConfigurationPagePath()
    {
        return All().Single().EmbeddedResourcePath;
    }
}
