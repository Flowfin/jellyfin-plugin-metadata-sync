using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Keeps the server's image surfaces out of this plugin's assembly.
/// </summary>
/// <remarks>
/// Image bytes never move. Not posters, not backdrops, not logos, not
/// thumbnails, not people's photographs. An image is stored as a file and served
/// as a file, so copying one is copying media between two servers by another
/// name, and a plugin that moves a few megabytes per item has disk, bandwidth
/// and partial writes as its failure modes rather than reconciliation.
///
/// What may move instead is the identity of an image rather than the image: a
/// provider identifier the receiving server's own image provider can fetch its
/// own copy from, with its own credentials and its own cache. So the guard has
/// to refuse the byte surfaces and leave the identifier route alone, and those
/// two sit next to each other on the same type.
///
/// The vocabulary is deliberately wider than the obvious call. A guard naming
/// only <c>IProviderManager.SaveImage</c> is walked around by reaching
/// <c>BaseItem.SetImagePath</c>, or by taking the bytes through
/// <c>IImageProcessor</c> and never naming the provider manager at all. Each
/// entry below is a way to the same bytes.
///
/// Two rules over one vocabulary. The naming rules below refuse these members
/// and types anywhere in the plugin, which is the wider subject and the weaker
/// property. The reachability rule refuses them where a pass could reach them,
/// which is the narrower subject and what #14 asks for in the words a code path.
/// Both are kept: the naming half reds a file that declares an image provider
/// nothing calls yet, and the reachability half is the one that still says
/// something on the day a legitimate reason to name one of these appears
/// somewhere the pass does not go.
///
/// What neither catches, stated rather than left to be assumed. A member reached
/// by reflection from a string spells none of these names, in either half. Image
/// removal is a different act and is <c>ItemDeletionTests</c>, not here. And
/// <c>ImageType</c> is left out of the set on purpose: it names which image slot
/// is meant and moves no bytes on its own, so refusing it would refuse a row
/// that only ever said which picture was being talked about.
/// </remarks>
public class ImageBytesTests
{
    /// <summary>
    /// The members that read or write image bytes, each as its declaring type
    /// and its name. Overloads collapse, which is what is wanted: a guard naming
    /// one overload of <c>SetImage</c> is walked around by calling another.
    /// </summary>
    private static readonly string[] ImageMembers =
    {
        // Writing an image through the provider plane. This is the call a
        // plugin that decided to carry artwork would reach for first.
        "MediaBrowser.Controller.Providers.IProviderManager.SaveImage",

        // Asking the plane which images a peer provider could supply. Listing
        // them is the step before fetching them.
        "MediaBrowser.Controller.Providers.IProviderManager.GetAvailableRemoteImages",

        // The same act through the library instead of the provider manager.
        // ConvertImageToLocal takes a remote image and lands it on this disk.
        "MediaBrowser.Controller.Library.ILibraryManager.ConvertImageToLocal",
        "MediaBrowser.Controller.Library.ILibraryManager.UpdateImagesAsync",

        // The item's own image surface, which skips both managers and keeps the
        // effect. Reading a path is how bytes are found; setting one is how
        // bytes are claimed.
        "MediaBrowser.Controller.Entities.BaseItem.AddImage",
        "MediaBrowser.Controller.Entities.BaseItem.GetImageInfo",
        "MediaBrowser.Controller.Entities.BaseItem.GetImagePath",
        "MediaBrowser.Controller.Entities.BaseItem.GetImages",
        "MediaBrowser.Controller.Entities.BaseItem.SetImage",
        "MediaBrowser.Controller.Entities.BaseItem.SetImagePath",
        "MediaBrowser.Controller.Entities.BaseItem.SwapImagesAsync",
        "MediaBrowser.Controller.Entities.BaseItem.ValidateImages",
    };

    /// <summary>
    /// The types that only somebody handling image bytes has a use for. Naming
    /// one is not moving an image, and it is what a file that is about to move
    /// one declares first.
    /// </summary>
    private static readonly string[] ImageTypes =
    {
        // The encoder and the processor are the bytes themselves.
        "MediaBrowser.Controller.Drawing.IImageEncoder",
        "MediaBrowser.Controller.Drawing.IImageProcessor",

        // The provider shapes. Implementing one of these is how a plugin
        // becomes a source of artwork for the server.
        "MediaBrowser.Controller.Providers.IDynamicImageProvider",
        "MediaBrowser.Controller.Providers.IImageProvider",
        "MediaBrowser.Controller.Providers.ILocalImageProvider",
        "MediaBrowser.Controller.Providers.IRemoteImageProvider",

        // The carriers. DynamicImageResponse holds a stream of image bytes and
        // ItemImageInfo holds where an item's image lives.
        "MediaBrowser.Controller.Entities.ItemImageInfo",
        "MediaBrowser.Controller.Providers.DynamicImageResponse",
        "MediaBrowser.Controller.Providers.LocalImageInfo",
    };

    /// <summary>
    /// The types a pass is made of, and therefore where the walk below starts.
    /// </summary>
    private const string ReconciliationPath = "Jellyfin.Plugin.MetadataSync.Reconciliation.";

    /// <summary>
    /// The rule. Nothing in the plugin assembly names a way to read or write
    /// image bytes.
    /// </summary>
    /// <remarks>
    /// This is the wider of the two rules and the weaker one. It says the
    /// vocabulary appears nowhere, which is easy to keep true while the plugin
    /// is small and is not what the issue asks about. The reachability rule
    /// below is the one written in the issue's own terms.
    /// </remarks>
    [Fact]
    public void ThePluginNamesNoWayToReadOrWriteImageBytes()
    {
        Assert.Empty(ImageMembersNamedBy(typeof(Plugin).Assembly));
    }

    /// <summary>
    /// The rule #14 asks for. No code path that starts in a pass arrives at a
    /// way to read or write image bytes.
    /// </summary>
    /// <remarks>
    /// The walk starts at every method on every type a pass is made of, follows
    /// each call into the assembly, and collects what the methods it arrives at
    /// name outside it. So this refuses an image read three helpers deep in a
    /// file whose own name says nothing about images, which is the shape a
    /// naming scan cannot tell from a legitimate one.
    /// </remarks>
    [Fact]
    public void NoWayToAnImageIsReachableFromAPass()
    {
        var reached = ImagesReachableFrom(typeof(Plugin).Assembly, OnTheReconciliationPath);

        Assert.Empty(reached.MembersAmong(ImageMembers));
        Assert.Empty(reached.TypesAmong(ImageTypes));
    }

    /// <summary>
    /// The walk starts somewhere. A predicate that matched no type would reach
    /// nothing and pass the rule above on any tree at all, including one where
    /// the reconciliation namespace had been renamed and the guard left behind.
    /// </summary>
    [Fact]
    public void TheWalkStartsFromThePassThatIsInTheTree()
    {
        var reached = ImagesReachableFrom(typeof(Plugin).Assembly, OnTheReconciliationPath);

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
        var reached = ImagesReachableFrom(typeof(Plugin).Assembly, OnTheReconciliationPath);

        Assert.True(reached.MethodsRead > 0);
        Assert.NotEmpty(reached.Members);
    }

    /// <summary>
    /// The bite, executed rather than argued. A fixture entry in this suite
    /// reaches the act two types away and names it nowhere itself, so a walk
    /// that stopped at the first call would report nothing and pass.
    /// </summary>
    [Fact]
    public void TheWalkFollowsACallChainAcrossTypes()
    {
        var reached = ImagesReachableFrom(
            typeof(ImageBytesTests).Assembly,
            name => string.Equals(name, typeof(ReachabilityEntryThatUsesAChain).FullName, StringComparison.Ordinal));

        Assert.Contains("MediaBrowser.Controller.Providers.IProviderManager.SaveImage", reached.MembersAmong(ImageMembers), StringComparer.Ordinal);
    }

    /// <summary>
    /// The leg that says this is a different question from the one the naming
    /// scan answers, and the reason a second reader was worth building. The same
    /// assembly names the act, and an entry that cannot reach it is not refused
    /// for it.
    /// </summary>
    [Fact]
    public void TheWalkDoesNotReportWhatTheAssemblyMerelyNames()
    {
        var assembly = typeof(ImageBytesTests).Assembly;
        var reached = ImagesReachableFrom(
            assembly,
            name => string.Equals(name, typeof(ReachabilityQuietEntry).FullName, StringComparison.Ordinal));

        Assert.Contains("MediaBrowser.Controller.Providers.IProviderManager.SaveImage", ImageMembersNamedBy(assembly), StringComparer.Ordinal);
        Assert.Empty(reached.MembersAmong(ImageMembers));
    }

    /// <summary>
    /// Dispatch is followed. The applier writes through a contract and never
    /// names the type that carries out the write, so a walk stopping at the
    /// interface member would read the one route to the library as unreachable
    /// and report a clean answer about a path it never entered.
    /// </summary>
    [Fact]
    public void TheWalkFollowsDispatchToAnImplementation()
    {
        var reached = ImagesReachableFrom(
            typeof(ImageBytesTests).Assembly,
            name => string.Equals(name, typeof(ReachabilityEntryThatDispatches).FullName, StringComparison.Ordinal));

        Assert.Contains("MediaBrowser.Controller.Providers.IProviderManager.SaveImage", reached.MembersAmong(ImageMembers), StringComparer.Ordinal);
    }

    /// <summary>
    /// And a body the compiler moved into a state machine is read. Every method
    /// on this plugin's write path is asynchronous, so a walk that read only the
    /// method as written would read almost none of what it claims to cover.
    /// </summary>
    [Fact]
    public void TheWalkFollowsIntoAnAsynchronousBody()
    {
        var reached = ImagesReachableFrom(
            typeof(ImageBytesTests).Assembly,
            name => string.Equals(name, typeof(ReachabilityEntryThatIsAsynchronous).FullName, StringComparison.Ordinal));

        Assert.Contains("MediaBrowser.Controller.Providers.IProviderManager.SaveImage", reached.MembersAmong(ImageMembers), StringComparer.Ordinal);
    }

    /// <summary>
    /// The same rule one step earlier. A type is named before a member on it is
    /// called, and an implemented provider interface names no member at all.
    /// </summary>
    [Fact]
    public void ThePluginNamesNoTypeThatCarriesAnImage()
    {
        Assert.Empty(ImageTypesNamedBy(typeof(Plugin).Assembly));
    }

    /// <summary>
    /// The scan reads references. If it ever reads none it passes for the wrong
    /// reason, so what it read is asserted before anything is concluded from a
    /// clean run.
    /// </summary>
    [Fact]
    public void TheScanActuallyReadsThePluginsReferences()
    {
        Assert.NotEmpty(AssemblyMetadata.MemberNames(typeof(Plugin).Assembly));
        Assert.NotEmpty(AssemblyMetadata.TypeNames(typeof(Plugin).Assembly));
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
        var named = ImageMembersNamedBy(typeof(ImageBytesTests).Assembly);

        Assert.Contains("MediaBrowser.Controller.Providers.IProviderManager.SaveImage", named, StringComparer.Ordinal);
    }

    /// <summary>
    /// The bite on the type half, for the same reason.
    /// </summary>
    [Fact]
    public void TheScanFindsTheCarrierInAnAssemblyThatDoesNameIt()
    {
        var named = ImageTypesNamedBy(typeof(ImageBytesTests).Assembly);

        Assert.Contains("MediaBrowser.Controller.Providers.DynamicImageResponse", named, StringComparer.Ordinal);
    }

    /// <summary>
    /// The neighbour, and it is the whole reason this guard is worth reading.
    /// What may move in place of an image is the identifier the receiving
    /// server's own image provider fetches its own copy from, and that read sits
    /// on the same type as the image reads refused above. A guard that matched a
    /// bare word like image, or that refused the item type wholesale, would
    /// refuse the row this plugin is supposed to carry.
    /// </summary>
    [Fact]
    public void TheScanAcceptsTheIdentifierRouteThatMayMoveInstead()
    {
        var assembly = typeof(ImageBytesTests).Assembly;
        var refused = ImageMembersNamedBy(assembly);
        var named = AssemblyMetadata.MemberNames(assembly);

        Assert.Contains("MediaBrowser.Controller.Entities.BaseItem.get_ProviderIds", named, StringComparer.Ordinal);
        Assert.DoesNotContain("MediaBrowser.Controller.Entities.BaseItem.get_ProviderIds", refused, StringComparer.Ordinal);
    }

    /// <summary>
    /// Every member and every type in the two sets is one the server actually
    /// has. A guard whose vocabulary has drifted from the server cannot fire,
    /// and it reads exactly like one that is passing.
    /// </summary>
    [Fact]
    public void EveryNameInTheSetsIsOneTheServerActuallyHas()
    {
        var unresolvedMembers = ImageMembers.Where(name => !ServerHasMember(name)).ToList();
        var unresolvedTypes = ImageTypes.Where(name => !ServerHasType(name)).ToList();

        Assert.Empty(unresolvedMembers);
        Assert.Empty(unresolvedTypes);
    }

    /// <summary>
    /// Names the member the bite leg needs this assembly to reference, and the
    /// identifier read the neighbour leg needs beside it. It is a delegate that
    /// is never invoked; the compiler emits the references either way, which is
    /// what the scan reads back.
    /// </summary>
    /// <returns>The act this plugin refuses, taken next to the read it allows.</returns>
    private static Func<IProviderManager, BaseItem, Dictionary<string, string>> TheActThisGuardIsAbout()
    {
        return static (providers, item) =>
        {
            _ = providers.SaveImage(
                item,
                "https://example.invalid/poster.jpg",
                MediaBrowser.Model.Entities.ImageType.Primary,
                null,
                System.Threading.CancellationToken.None);

            return item.ProviderIds;
        };
    }

    /// <summary>
    /// Names the carrier the type bite leg needs this assembly to reference.
    /// </summary>
    /// <returns>A carrier of image bytes, never constructed by anything that runs.</returns>
    private static Func<DynamicImageResponse> TheCarrierThisGuardIsAbout()
    {
        return static () => new DynamicImageResponse();
    }

    /// <summary>
    /// Whether a type is one a pass is made of. The reconciliation namespace is
    /// the whole answer: what a pass reaches beyond it, the register it reads
    /// and the comparison rules it resolves through, is reached by the walk
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
    private static AssemblyReachability.Reached ImagesReachableFrom(Assembly assembly, Func<string, bool> isEntryType)
    {
        return AssemblyReachability.From(assembly, isEntryType);
    }

    /// <summary>
    /// Runs the member set over one assembly and returns what it names, sorted
    /// so a failure reads the same way twice.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The refused members that assembly names.</returns>
    private static IReadOnlyList<string> ImageMembersNamedBy(Assembly assembly)
    {
        _ = TheActThisGuardIsAbout();
        var named = AssemblyMetadata.MemberNames(assembly);

        return ImageMembers.Where(named.Contains).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Runs the type set over one assembly and returns what it names, sorted so
    /// a failure reads the same way twice.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The refused types that assembly names.</returns>
    private static IReadOnlyList<string> ImageTypesNamedBy(Assembly assembly)
    {
        _ = TheCarrierThisGuardIsAbout();
        var named = AssemblyMetadata.TypeNames(assembly);

        return ImageTypes.Where(named.Contains).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Resolves a declaring type and a member name against the server
    /// assemblies, so the set cannot name something that is not there.
    /// </summary>
    /// <param name="qualified">The declaring type's full name, a full stop and the member's name.</param>
    /// <returns>Whether the server has it.</returns>
    private static bool ServerHasMember(string qualified)
    {
        var split = qualified.LastIndexOf('.');
        var type = ServerType(qualified[..split]);

        return type is not null && type.GetMember(qualified[(split + 1)..]).Length > 0;
    }

    /// <summary>
    /// Resolves a type name against the server assemblies.
    /// </summary>
    /// <param name="full">The type's full name.</param>
    /// <returns>Whether the server has it.</returns>
    private static bool ServerHasType(string full)
    {
        return ServerType(full) is not null;
    }

    /// <summary>
    /// Looks a type up in the two server assemblies this plugin references. The
    /// image surfaces are split across both, so one lookup would find half the
    /// set and report the other half as drift.
    /// </summary>
    /// <param name="full">The type's full name.</param>
    /// <returns>The type, or null where neither assembly has it.</returns>
    private static Type? ServerType(string full)
    {
        return typeof(BaseItem).Assembly.GetType(full, throwOnError: false)
            ?? typeof(MediaBrowser.Model.Entities.ImageType).Assembly.GetType(full, throwOnError: false);
    }
}
