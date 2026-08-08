using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Keeps the user-scoped surface out of this plugin's assembly.
/// </summary>
/// <remarks>
/// This plugin reconciles what a library says about a work. What a server holds
/// about a person, which is played state, playback position, favourites and a
/// personal rating, belongs to the sibling plugin and is never read or written
/// here. The temptation is real rather than theoretical: a user's personal
/// rating looks like a field on an item, and the item's community rating is a
/// field the register already carries, and the two are one word apart in the
/// model.
///
/// The guard is an assembly scan rather than a source scan, and the difference
/// is the point. A source scan cannot see a reference that arrives through a
/// transitive helper, an extension method or a generic argument written
/// somewhere else. A type reference is emitted into the assembly's metadata by
/// whichever file names the type, so the scan below finds it wherever it was
/// written.
///
/// What it cannot catch, stated rather than left to be assumed. A type reached
/// only by reflection from a string spells no type reference and passes. So does
/// a value of one of these types obtained from a call that returns
/// <c>object</c>. And the scan reads the plugin assembly this test run built, so
/// it says nothing about a build made with different options. It refuses the
/// naming and never the reachability, which is a weaker property than the one
/// #18 states in words, and #20 owns the reachability half for the transport
/// surface.
/// </remarks>
public class UserDataSurfaceTests
{
    /// <summary>
    /// The surface, named as the server names it. Each entry is a type that
    /// exists to carry what one person has done with an item, and every one of
    /// them is resolved against the server assemblies below, so a server line
    /// that renames one turns this into a red suite rather than into a scan for
    /// a string nothing can match.
    /// </summary>
    /// <remarks>
    /// <c>IUserManager</c> is deliberately absent. It is the account surface
    /// rather than the per-item state surface, and which local users a pairing
    /// maps is a question the pairing contract answers rather than one #18
    /// settles. Adding it here would refuse work this plan has not refused.
    /// </remarks>
    private static readonly string[] UserScopedTypes =
    {
        // The interface the whole surface hangs off, and the one #18 names.
        "MediaBrowser.Controller.Library.IUserDataManager",

        // The state itself: played, playback position, favourite, rating.
        "MediaBrowser.Controller.Entities.UserItemData",

        // The store behind it. Reaching this skips the manager and keeps the
        // same data, which is what a guard naming only the manager would miss.
        "MediaBrowser.Controller.Persistence.IUserDataRepository",

        // The event raised when that state changes, and the reason enum on it.
        // Subscribing is reading, and it arrives without anybody calling a
        // getter.
        "MediaBrowser.Controller.Library.UserDataSaveEventArgs",
        "MediaBrowser.Model.Entities.UserDataSaveReason",

        // The transport shapes of the same state. A payload carrying one of
        // these is the user-scoped field arriving on the contract, which is the
        // case #18 refuses on the receiving side.
        "MediaBrowser.Model.Dto.UserItemDataDto",
        "MediaBrowser.Model.Dto.UpdateUserItemDataDto",
        "MediaBrowser.Model.Session.UserDataChangeInfo",
    };

    /// <summary>
    /// The rule. Nothing in the plugin assembly names any of the types above.
    /// </summary>
    /// <remarks>
    /// It passes today because nothing references them, which is the honest
    /// reason and not a proof of anything. What it buys is that the first commit
    /// that changes the answer is refused by the suite instead of by whoever
    /// reviews it, and that is cheapest to install while the answer is still
    /// trivially yes.
    /// </remarks>
    [Fact]
    public void ThePluginNamesNothingScopedToAUser()
    {
        var named = UserScopedTypesNamedBy(typeof(Plugin).Assembly);

        Assert.Empty(named);
    }

    /// <summary>
    /// The scan reads type references. If it ever reads none it passes for the
    /// wrong reason, so the count is asserted before anything is concluded from
    /// a clean run.
    /// </summary>
    [Fact]
    public void TheScanActuallyReadsThePluginsTypeReferences()
    {
        Assert.NotEmpty(TypeNamesReferencedBy(typeof(Plugin).Assembly));
    }

    /// <summary>
    /// The bite, and it is executed rather than argued. This suite's own
    /// assembly names <see cref="TheSurfaceThisGuardIsAbout"/>, so running the
    /// same scan over it has to find the same type the rule above looks for. A
    /// scan that matched nothing anywhere would pass the rule above on any tree
    /// at all, and it fails here instead.
    /// </summary>
    [Fact]
    public void TheScanFindsTheSurfaceInAnAssemblyThatDoesNameIt()
    {
        var named = UserScopedTypesNamedBy(typeof(UserDataSurfaceTests).Assembly);

        Assert.Contains("MediaBrowser.Controller.Library.IUserDataManager", named, StringComparer.Ordinal);
    }

    /// <summary>
    /// Every name in the set is a type the server actually has. A guard whose
    /// vocabulary has drifted from the server is a guard that cannot fire, and
    /// it reads exactly like one that is passing.
    /// </summary>
    [Fact]
    public void EveryNameInTheSetIsATypeTheServerActuallyHas()
    {
        var unresolved = UserScopedTypes.Where(name => ServerType(name) is null).ToList();

        Assert.Empty(unresolved);
    }

    /// <summary>
    /// Names the type the bite leg needs this assembly to reference. The
    /// compiler emits a type reference for it, which is what
    /// <see cref="TheScanFindsTheSurfaceInAnAssemblyThatDoesNameIt"/> reads back
    /// out of the metadata.
    /// </summary>
    /// <returns>The user data surface this plugin refuses to hold.</returns>
    private static Type TheSurfaceThisGuardIsAbout()
    {
        return typeof(MediaBrowser.Controller.Library.IUserDataManager);
    }

    /// <summary>
    /// Resolves a server type by its full name, across the assemblies this
    /// suite already loads, so the set above cannot name something that is not
    /// there.
    /// </summary>
    private static Type? ServerType(string fullName)
    {
        var servers = new[]
        {
            TheSurfaceThisGuardIsAbout().Assembly,
            typeof(MediaBrowser.Model.Entities.MetadataField).Assembly,
        };

        return servers.Select(assembly => assembly.GetType(fullName, throwOnError: false)).FirstOrDefault(type => type is not null);
    }

    /// <summary>
    /// Runs the set over one assembly and returns what it names, sorted so a
    /// failure reads the same way twice.
    /// </summary>
    private static IReadOnlyList<string> UserScopedTypesNamedBy(Assembly assembly)
    {
        var referenced = TypeNamesReferencedBy(assembly);

        return UserScopedTypes.Where(referenced.Contains).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Reads the type names an assembly's metadata carries: the ones it refers
    /// to in another assembly, and the ones it declares itself. The first is
    /// what a use of a server type produces; the second is here so a type
    /// declared under one of these names in this plugin is caught too.
    /// </summary>
    private static HashSet<string> TypeNamesReferencedBy(Assembly assembly)
    {
        var location = assembly.Location;
        Assert.False(string.IsNullOrEmpty(location), "The assembly under test has no file on disk to read.");

        using var file = File.OpenRead(location);
        using var portableExecutable = new PEReader(file);
        var metadata = portableExecutable.GetMetadataReader();

        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in metadata.TypeReferences)
        {
            var reference = metadata.GetTypeReference(handle);
            names.Add(FullName(metadata.GetString(reference.Namespace), metadata.GetString(reference.Name)));
        }

        foreach (var handle in metadata.TypeDefinitions)
        {
            var definition = metadata.GetTypeDefinition(handle);
            names.Add(FullName(metadata.GetString(definition.Namespace), metadata.GetString(definition.Name)));
        }

        return names;
    }

    private static string FullName(string @namespace, string name)
    {
        return string.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name;
    }
}
