using System;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Store;
using MediaBrowser.Controller.Entities;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Removing what this plugin recorded does not touch the library.
/// </summary>
/// <remarks>
/// An operator asking for a pairing's records to be gone may mean either of two
/// things, and this plugin does one of them. It lets go of what it wrote down.
/// It does not put values back, and it does not remove items: metadata already
/// written stays on the items it was written to, which is #66 and is stated on
/// the report itself so the operator reads it before they decide.
/// <para>
/// The rule is worth a walk rather than a sentence because the failure is
/// plausible. Whoever builds the revert in #64 works in the same files, and a
/// removal that reached the library would be a plugin deleting somebody's
/// metadata because they asked to delete a record of it. A name scan cannot say
/// this: the plugin legitimately writes to a library one type away, so what is
/// asked here is whether the removal path arrives there, which is a question
/// about the call graph.
/// </para>
/// <para>
/// The bound is <see cref="AssemblyReachability"/>'s own and is stated at that
/// type. What matters here: a member reached by reflection from a string spells
/// no token and is invisible, and the walk stops at the assembly boundary, so a
/// server method that itself touches an item is the server's business.
/// </para>
/// </remarks>
public class RemovalReachabilityTests
{
    /// <summary>
    /// The types a removal must not arrive at. The library, the item it holds,
    /// and the two ways an item is changed or taken away.
    /// </summary>
    private static readonly string[] LibraryTypes =
    {
        "MediaBrowser.Controller.Library.ILibraryManager",
        "MediaBrowser.Controller.Library.DeleteOptions",
        "MediaBrowser.Controller.Persistence.IItemRepository",
        "MediaBrowser.Controller.Entities.BaseItem",
    };

    /// <summary>
    /// The rule. Nothing a removal reaches is a way to the library.
    /// </summary>
    [Fact]
    public void NoLibraryTypeIsReachableFromARemoval()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnTheRemovalPath);

        Assert.Empty(reached.TypesAmong(LibraryTypes));
    }

    /// <summary>
    /// The walk starts somewhere. A predicate matching no type reaches nothing
    /// and passes the rule above on any tree at all, which is the way this
    /// guard would go quiet if the removal path were renamed.
    /// </summary>
    [Fact]
    public void TheWalkStartsFromTheRemovalThatIsInTheTree()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnTheRemovalPath);

        Assert.Contains(typeof(PairingStores).FullName!, reached.EntryTypes, StringComparer.Ordinal);
    }

    /// <summary>
    /// The walk reaches the store behind the interface. The removal calls an
    /// interface member, so a walk that stopped at the declaration would read
    /// none of what actually runs, and this plugin's one store would be outside
    /// the guard while the guard reported success.
    /// </summary>
    [Fact]
    public void TheWalkFollowsTheRemovalIntoTheStoreBehindTheInterface()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnTheRemovalPath);

        Assert.Contains("System.IO.File", reached.Types, StringComparer.Ordinal);
    }

    /// <summary>
    /// And the leg that says this is not a name scan under another name. The
    /// same assembly reaches the library from the write path, and the removal is
    /// not refused for it.
    /// </summary>
    [Fact]
    public void TheWalkDoesNotRefuseALibraryCallItCannotReach()
    {
        var assembly = typeof(Plugin).Assembly;

        var fromAPass = AssemblyReachability.From(
            assembly,
            name => name.StartsWith("Jellyfin.Plugin.MetadataSync.Reconciliation.", StringComparison.Ordinal));

        Assert.NotEmpty(fromAPass.TypesAmong(LibraryTypes));
    }

    /// <summary>
    /// Every type in the set is one that exists. A vocabulary that has drifted
    /// from the server cannot fire, and it reads exactly like one that is
    /// passing. The two lines this plugin is built against declare these four
    /// alike, and the walk is run against whichever one this target compiled
    /// with.
    /// </summary>
    [Fact]
    public void EveryNameInTheSetIsOneThatExists()
    {
        Assert.Empty(LibraryTypes.Where(name => Resolve(name) is null).ToList());
    }

    private static bool OnTheRemovalPath(string name)
    {
        return string.Equals(name, typeof(PairingStores).FullName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Looks a type up in the assemblies that could declare one of these names.
    /// </summary>
    /// <param name="full">The type's full name.</param>
    /// <returns>The type, or null where neither has it.</returns>
    private static Type? Resolve(string full)
    {
        return Type.GetType(full)
            ?? typeof(BaseItem).Assembly.GetType(full)
            ?? typeof(MediaBrowser.Model.Entities.MetadataField).Assembly.GetType(full);
    }
}
