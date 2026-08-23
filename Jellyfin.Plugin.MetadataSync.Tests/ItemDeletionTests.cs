using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Keeps the library's delete surface out of this plugin's assembly.
/// </summary>
/// <remarks>
/// The worst thing this plugin could do is remove something an operator wrote
/// themselves, and the likeliest route to it is a well-meant cleanup: an
/// uninstall that tidies up after itself, a revert that reaches past the fields
/// this plugin wrote, or a removal that treats an unattributed field as one of
/// ours. Removing a field's value and removing the item that holds it are
/// different acts, and only the first is ever in scope here.
///
/// So this guard is about the second act only. It refuses the naming of the
/// library's item deletion members, and it refuses the file deletion helpers
/// beside them, because a plugin whose non-goals include copying a media file
/// has no reason to delete one either.
///
/// It is two rules rather than one, and the difference is what #66 asks about.
/// The naming rule says the vocabulary appears nowhere in the plugin, which is
/// the wider statement and the weaker one: it is easy to keep true while the
/// plugin is small, and it is not a statement about a code path. The
/// reachability rule starts at the types a pass is made of, follows each call
/// into the assembly, and asks what the methods it arrives at name outside it,
/// so a removal several helpers deep in a file whose own name says nothing
/// about removing is refused. That is the rule written in the issue's own
/// terms.
///
/// What neither catches, stated rather than left to be assumed. A delete
/// invoked by reflection from a string spells none of these names, and neither
/// does one reached through an interface this plugin declares itself that
/// something outside the plugin assembly implements over the library. The walk
/// reads the assembly this run built, so it says nothing about a build made
/// with different options. And it says nothing about the other half of #66,
/// which is that a revert skips every field with no attribution record. That
/// half needs the record from #47 and cannot be written against a tree with no
/// store in it.
/// </remarks>
public class ItemDeletionTests
{
    /// <summary>
    /// The members that remove something, each as its declaring type and its
    /// name. Overloads collapse, which is what is wanted: a guard naming one
    /// overload of <c>DeleteItem</c> is walked around by calling another.
    /// </summary>
    private static readonly string[] RemovalMembers =
    {
        // The library's own item deletion, all three overloads.
        "MediaBrowser.Controller.Library.ILibraryManager.DeleteItem",

        // The same act one layer down, which skips the manager and keeps the
        // effect. A guard naming only the manager would miss it. The 12.0 line
        // no longer declares it, which is why it is also in
        // <see cref="RemovalMembersOnlyTheOlderLineDeclares"/> below.
        "MediaBrowser.Controller.Persistence.IItemRepository.DeleteItem",

        // Removing an image is removing a file. Image bytes never move here and
        // there is no reason for them to be removed here either.
        "MediaBrowser.Controller.Entities.BaseItem.DeleteImageAsync",

        // The file helpers beside them. Nothing in this plugin has a reason to
        // remove a file or a folder from an operator's disk.
        "MediaBrowser.Controller.IO.FileSystemHelper.DeleteFile",
        "MediaBrowser.Controller.IO.FileSystemHelper.DeleteEmptyFolders",
    };

    /// <summary>
    /// The members of the set above that exist on the 10.11 line and not on the
    /// 12.0 line.
    /// </summary>
    /// <remarks>
    /// The set stays refused on both lines. A member the newer server dropped is
    /// still a member the older one carries, and a plugin naming it is naming a
    /// removal whichever line it is compiled for, so nothing is taken out of the
    /// rule for a line.
    /// <para>
    /// What this list is for is the leg that asks whether the vocabulary is
    /// real. That question has a different answer per line, and asking it
    /// against one line only is how a set that has drifted goes on reading like
    /// one that bites. It is held in both directions below, so it can neither
    /// name something the older line never had nor keep naming something the
    /// newer line still declares.
    /// </para>
    /// </remarks>
    private static readonly string[] RemovalMembersOnlyTheOlderLineDeclares =
    {
        "MediaBrowser.Controller.Persistence.IItemRepository.DeleteItem",
    };

    /// <summary>
    /// The type that only a caller of the library's delete has any use for.
    /// Naming it is not deleting, and it is the argument somebody assembles one
    /// line before they do.
    /// </summary>
    private const string RemovalOptions = "MediaBrowser.Controller.Library.DeleteOptions";

    /// <summary>
    /// The types a pass is made of, and therefore where the walk below starts.
    /// </summary>
    private const string ReconciliationPath = "Jellyfin.Plugin.MetadataSync.Reconciliation.";

    /// <summary>
    /// The rule. Nothing in the plugin assembly names a way to remove an item.
    /// </summary>
    /// <remarks>
    /// This is the wider of the two rules and the weaker one. It says the
    /// vocabulary appears nowhere, which was once true because the plugin had
    /// no reconciliation path at all, and there is one now. The reachability
    /// rule below is the one written in this issue's own terms.
    /// </remarks>
    [Fact]
    public void ThePluginNamesNoWayToRemoveAnItem()
    {
        Assert.Empty(RemovalNamedBy(typeof(Plugin).Assembly));
    }

    /// <summary>
    /// The rule #66 asks for. No code path that starts in a pass arrives at a
    /// way to remove an item.
    /// </summary>
    /// <remarks>
    /// The walk starts at every method on every type a pass is made of, follows
    /// each call into the assembly, and collects what the methods it arrives at
    /// name outside it. So this refuses a removal several helpers deep in a file
    /// whose own name says nothing about removing, which is the shape a naming
    /// scan cannot tell from a legitimate call.
    /// </remarks>
    [Fact]
    public void NoWayToRemoveAnItemIsReachableFromAPass()
    {
        var reached = RemovalReachableFrom(typeof(Plugin).Assembly, OnTheReconciliationPath);

        Assert.Empty(reached.MembersAmong(RemovalMembers));
    }

    /// <summary>
    /// The walk starts somewhere. A predicate that matched no type would reach
    /// nothing and pass the rule above on any tree at all, including one where
    /// the reconciliation namespace had been renamed and the guard left behind.
    /// </summary>
    [Fact]
    public void TheWalkStartsFromThePassThatIsInTheTree()
    {
        var reached = RemovalReachableFrom(typeof(Plugin).Assembly, OnTheReconciliationPath);

        Assert.Contains("Jellyfin.Plugin.MetadataSync.Reconciliation.Planner", reached.EntryTypes, StringComparer.Ordinal);
        Assert.Contains("Jellyfin.Plugin.MetadataSync.Reconciliation.Applier", reached.EntryTypes, StringComparer.Ordinal);
        Assert.Contains("Jellyfin.Plugin.MetadataSync.Reconciliation.LibraryPlanTarget", reached.EntryTypes, StringComparer.Ordinal);
    }

    /// <summary>
    /// And it decodes bodies. A walk that read no instructions would report an
    /// empty answer for the same reason a walk with no entry does.
    /// </summary>
    [Fact]
    public void TheWalkReadsInstructionsRatherThanNone()
    {
        var reached = RemovalReachableFrom(typeof(Plugin).Assembly, OnTheReconciliationPath);

        Assert.True(reached.MethodsRead > 0);
        Assert.NotEmpty(reached.Members);
    }

    /// <summary>
    /// The bite, executed rather than argued. A fixture entry in this suite
    /// reaches the removal two types away and names it nowhere itself, so a walk
    /// that stopped at the first call would report nothing and pass.
    /// </summary>
    [Fact]
    public void TheWalkFollowsACallChainAcrossTypes()
    {
        var reached = RemovalReachableFrom(
            typeof(ItemDeletionTests).Assembly,
            name => string.Equals(name, typeof(ReachabilityEntryThatRemovesAnItem).FullName, StringComparison.Ordinal));

        Assert.Contains(
            "MediaBrowser.Controller.Library.ILibraryManager.DeleteItem",
            reached.MembersAmong(RemovalMembers),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The leg that says this is a different question from the one the naming
    /// scan answers, and the reason the second reader was worth adding. The same
    /// assembly names the act, and an entry that cannot reach it is not refused
    /// for it.
    /// </summary>
    [Fact]
    public void TheWalkDoesNotReportWhatTheAssemblyMerelyNames()
    {
        var assembly = typeof(ItemDeletionTests).Assembly;
        var reached = RemovalReachableFrom(
            assembly,
            name => string.Equals(name, typeof(ReachabilityQuietEntry).FullName, StringComparison.Ordinal));

        Assert.Contains("MediaBrowser.Controller.Library.ILibraryManager.DeleteItem", RemovalNamedBy(assembly), StringComparer.Ordinal);
        Assert.Empty(reached.MembersAmong(RemovalMembers));
    }

    /// <summary>
    /// The scan reads member references. If it ever reads none it passes for
    /// the wrong reason, so what it read is asserted before anything is
    /// concluded from a clean run.
    /// </summary>
    [Fact]
    public void TheScanActuallyReadsThePluginsMemberReferences()
    {
        Assert.NotEmpty(AssemblyMetadata.MemberNames(typeof(Plugin).Assembly));
    }

    /// <summary>
    /// The bite, executed rather than argued. This suite's own assembly names
    /// <see cref="TheActThisGuardIsAbout"/>, so the same scan run over it has to
    /// find the same member the rule above looks for. A scan that matched
    /// nothing anywhere would pass that rule on any tree at all.
    /// </summary>
    [Fact]
    public void TheScanFindsTheActInAnAssemblyThatDoesNameIt()
    {
        var named = RemovalNamedBy(typeof(ItemDeletionTests).Assembly);

        Assert.Contains("MediaBrowser.Controller.Library.ILibraryManager.DeleteItem", named, StringComparer.Ordinal);
    }

    /// <summary>
    /// The neighbour, and it is the whole reason this guard is worth reading. A
    /// write through the library is what this plugin is for, and it is one word
    /// from the call above. A scan that refused the library manager, or that
    /// matched a bare word like delete, would refuse the work rather than the
    /// mistake.
    /// </summary>
    [Fact]
    public void TheScanAcceptsTheWriteThisPluginExistsToMake()
    {
        var named = RemovalNamedBy(typeof(ItemDeletionTests).Assembly);

        Assert.DoesNotContain("MediaBrowser.Controller.Library.ILibraryManager.UpdateItemAsync", named, StringComparer.Ordinal);
        Assert.Contains("MediaBrowser.Controller.Library.ILibraryManager.UpdateItemAsync", TheNeighbourThisGuardMustNotRefuse(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Every member and the type in the set is one the server actually has. A
    /// guard whose vocabulary has drifted from the server cannot fire, and it
    /// reads exactly like one that is passing.
    /// </summary>
    [Fact]
    public void EveryNameInTheSetIsOneTheServerActuallyHas()
    {
#if NET9_0
        var expected = RemovalMembers;
#else
        var expected = RemovalMembers
            .Where(name => !RemovalMembersOnlyTheOlderLineDeclares.Contains(name, StringComparer.Ordinal));
#endif

        var unresolved = expected.Where(name => !ServerHasMember(name)).ToList();

        Assert.True(
            unresolved.Count == 0,
            "These names in the removal set do not resolve against the server this target compiles against: "
                + string.Join(", ", unresolved));

        Assert.NotNull(typeof(ILibraryManager).Assembly.GetType(RemovalOptions, throwOnError: false));
    }

#if NET9_0
    /// <summary>
    /// The exception list held against the server, in the direction that keeps
    /// it from growing. A member named here that the older line does not
    /// declare either was never real, and listing it excuses the leg above from
    /// resolving a name nothing ever had.
    /// </summary>
    [Fact]
    public void EveryMemberExcusedOnTheNewerLineIsOneTheOlderLineHas()
    {
        var missing = RemovalMembersOnlyTheOlderLineDeclares.Where(name => !ServerHasMember(name)).ToList();

        Assert.True(
            missing.Count == 0,
            "These names are excused on the newer line and the older line does not declare them either: "
                + string.Join(", ", missing));
    }

#else
    /// <summary>
    /// The same list held in the other direction, which is the one that rots
    /// quietly. A member the newer line brings back is excused here forever
    /// afterwards, and the leg above cannot see it because that leg is about
    /// the older line.
    /// </summary>
    [Fact]
    public void NoMemberExcusedOnTheNewerLineIsOneTheNewerLineStillHas()
    {
        var returned = RemovalMembersOnlyTheOlderLineDeclares.Where(ServerHasMember).ToList();

        Assert.True(
            returned.Count == 0,
            "These names are excused on the newer line and the newer line declares them: " + string.Join(", ", returned));
    }
#endif

    /// <summary>
    /// The list is not allowed to be empty on either line. An empty one
    /// satisfies both legs above by having nothing to disagree with, and that
    /// is the state this file was in before the second line was compiled here:
    /// one set, asked of one server, with the difference between the lines
    /// unread.
    /// </summary>
    [Fact]
    public void TheMembersOnlyTheOlderLineDeclaresAreAllInTheRefusedSet()
    {
        Assert.NotEmpty(RemovalMembersOnlyTheOlderLineDeclares);

        var outside = RemovalMembersOnlyTheOlderLineDeclares
            .Where(name => !RemovalMembers.Contains(name, StringComparer.Ordinal)).ToList();

        Assert.True(
            outside.Count == 0,
            "These names are excused on the newer line and are not in the refused set at all: " + string.Join(", ", outside));
    }

    /// <summary>
    /// The type the removal takes as an argument is not named either. Naming it
    /// is one line before the call, and it is the cheaper thing to catch.
    /// </summary>
    [Fact]
    public void ThePluginDoesNotNameTheRemovalOptions()
    {
        Assert.DoesNotContain(RemovalOptions, AssemblyMetadata.TypeNames(typeof(Plugin).Assembly), StringComparer.Ordinal);
    }

    /// <summary>
    /// Names the member the bite leg needs this assembly to reference. It is a
    /// delegate that is never invoked; the compiler emits the member reference
    /// either way, which is what the scan reads back.
    /// </summary>
    /// <returns>The act this plugin refuses.</returns>
    private static Action<ILibraryManager, BaseItem, DeleteOptions> TheActThisGuardIsAbout()
    {
        return static (library, item, options) => library.DeleteItem(item, options);
    }

    /// <summary>
    /// Names the write this plugin exists to make, so the neighbour leg is
    /// comparing against something this assembly really does name.
    /// </summary>
    /// <returns>The member names this assembly carries.</returns>
    private static HashSet<string> TheNeighbourThisGuardMustNotRefuse()
    {
        _ = TheWriteThisPluginExistsToMake();
        return AssemblyMetadata.MemberNames(typeof(ItemDeletionTests).Assembly);
    }

    private static Func<ILibraryManager, BaseItem, System.Threading.Tasks.Task> TheWriteThisPluginExistsToMake()
    {
        return static (library, item) => library.UpdateItemAsync(
            item,
            item,
            ItemUpdateType.MetadataEdit,
            System.Threading.CancellationToken.None);
    }

    /// <summary>
    /// Whether a type is one a pass is made of. The reconciliation namespace is
    /// the whole answer: what a pass reaches beyond it is reached by the walk
    /// rather than listed here, which is the difference between an entry and a
    /// subject.
    /// </summary>
    /// <param name="name">The full name of a type in the plugin.</param>
    /// <returns>Whether the walk starts there.</returns>
    private static bool OnTheReconciliationPath(string name)
    {
        return name.StartsWith(ReconciliationPath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Walks one assembly from the entries a predicate names.
    /// </summary>
    /// <param name="assembly">The assembly to walk.</param>
    /// <param name="isEntryType">Which types the walk starts at.</param>
    /// <returns>What it reached.</returns>
    private static AssemblyReachability.Reached RemovalReachableFrom(Assembly assembly, Func<string, bool> isEntryType)
    {
        return AssemblyReachability.From(assembly, isEntryType);
    }

    /// <summary>
    /// Runs the set over one assembly and returns what it names, sorted so a
    /// failure reads the same way twice.
    /// </summary>
    private static IReadOnlyList<string> RemovalNamedBy(Assembly assembly)
    {
        var named = AssemblyMetadata.MemberNames(assembly);

        return RemovalMembers.Where(named.Contains).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Resolves a declaring type and a member name against the server
    /// assemblies, so the set cannot name something that is not there.
    /// </summary>
    private static bool ServerHasMember(string qualified)
    {
        var split = qualified.LastIndexOf('.');
        var declaring = qualified[..split];
        var member = qualified[(split + 1)..];

        var type = typeof(ILibraryManager).Assembly.GetType(declaring, throwOnError: false);

        return type is not null && type.GetMember(member).Length > 0;
    }
}
