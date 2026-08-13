using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MediaBrowser.Controller.Entities;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Keeps the file system out of the decision that two items are the same.
/// </summary>
/// <remarks>
/// Every published attempt at this problem derives item identity from the
/// filesystem and every one of them breaks the same way. One matches content by
/// file path and supports only single-folder libraries. One requires media
/// identifiers to be identical on both instances, which, because the server
/// derives them from the path and the filename, means the directory structure
/// and the naming have to match on both servers. The assumption underneath all
/// of them is that two servers hold the same files laid out the same way, which
/// is true of one server copied twice and false of two households that each
/// built a library. That second case is the one this plugin exists for.
/// <para>
/// So no path, no filename, no directory name, no size and no hash of a file
/// takes part in deciding that two items are the same. Where the provider
/// identifiers cannot answer, the answer is that it does not resolve, and #29
/// says what happens then.
/// </para>
/// <para>
/// This is the walk #28 asks for and it is a different question from the lint
/// that has stood in for it. `no-file-system-property-in-item-identity` refuses
/// these spellings anywhere in the plugin's sources, which is wider and weaker:
/// it matches text rather than following a read, and its own record says
/// reachability is what it cannot catch. This starts at the types that resolve
/// and asks what they arrive at, so a filename read three helpers deep in a file
/// whose name says nothing about files is refused, and a legitimate read
/// somewhere no resolution goes is not.
/// </para>
/// <para>
/// What it cannot catch is the reader's bound and is the same one for every
/// caller of the walk. A property read by reflection from a string spells no
/// token. A value taken off the file system somewhere else and handed in as a
/// plain string is not a file-system read at the site that receives it, and
/// nothing here distinguishes it from any other string.
/// </para>
/// </remarks>
public class ResolutionPathTests
{
    /// <summary>
    /// The types that decide whether two items are the same, and therefore where
    /// the walk starts. Matching holds the identifier comparison and the
    /// parent-plus-ordinal rule; References holds the same question asked about
    /// a person, a studio or a genre. Both are resolution and both are seeded,
    /// because a rule that held for one and not the other would be a rule an
    /// operator could not state.
    /// </summary>
    private static readonly string[] ResolutionPaths =
    {
        "Jellyfin.Plugin.MetadataSync.Matching.",
        "Jellyfin.Plugin.MetadataSync.References.",
    };

    /// <summary>
    /// The members that answer a question about a file. Each is a property on
    /// the item that the server filled in from the disk, and reading one is how
    /// an identity comes to depend on where a file happens to sit.
    /// </summary>
    private static readonly string[] FileSystemMembers =
    {
        // Where the file is, in three spellings. The last is the one every
        // prior attempt reaches for, because it looks like a title.
        "MediaBrowser.Controller.Entities.BaseItem.get_Path",
        "MediaBrowser.Controller.Entities.BaseItem.get_ContainingFolderPath",
        "MediaBrowser.Controller.Entities.BaseItem.get_FileNameWithoutExtension",

        // Where the server keeps its own files for the item. A path all the
        // same, and one that differs between two servers for reasons that have
        // nothing to do with the work.
        "MediaBrowser.Controller.Entities.BaseItem.GetInternalMetadataPath",
    };

    /// <summary>
    /// The types that only somebody asking the file system something has a use
    /// for. Naming one is not deciding an identity, and it is what a file about
    /// to decide one from a path declares first.
    /// </summary>
    private static readonly string[] FileSystemTypes =
    {
        // The framework's own file surface. Path is the one that matters most:
        // it is how a filename is turned into something that looks like a name.
        "System.IO.Directory",
        "System.IO.DirectoryInfo",
        "System.IO.File",
        "System.IO.FileInfo",
        "System.IO.FileSystemInfo",
        "System.IO.Path",

        // The server's own, which reaches the same disk without naming
        // System.IO at all.
        "MediaBrowser.Model.IO.IFileSystem",
        "MediaBrowser.Model.IO.FileSystemMetadata",

        // A hash of a file is a file-system property with an extra step, and it
        // is the one somebody proposes when the path argument is lost.
        "System.Security.Cryptography.HashAlgorithm",
        "System.Security.Cryptography.MD5",
        "System.Security.Cryptography.SHA1",
        "System.Security.Cryptography.SHA256",
    };

    /// <summary>
    /// The rule. No code path that starts in a resolution arrives at a read of
    /// the file system.
    /// </summary>
    [Fact]
    public void NoFileSystemPropertyIsReachableFromAResolution()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnTheResolutionPath);

        Assert.Empty(reached.MembersAmong(FileSystemMembers));
        Assert.Empty(reached.TypesAmong(FileSystemTypes));
    }

    /// <summary>
    /// The walk starts somewhere. A predicate matching no type reaches nothing
    /// and passes the rule above on any tree at all, including one where the
    /// namespaces moved and the guard stayed behind.
    /// </summary>
    [Fact]
    public void TheWalkStartsFromTheResolversThatAreInTheTree()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnTheResolutionPath);

        Assert.Contains("Jellyfin.Plugin.MetadataSync.Matching.ProviderIdentifiers", reached.EntryTypes, StringComparer.Ordinal);
        Assert.Contains("Jellyfin.Plugin.MetadataSync.Matching.OrdinalResolver", reached.EntryTypes, StringComparer.Ordinal);
        Assert.Contains("Jellyfin.Plugin.MetadataSync.References.ReferenceResolver", reached.EntryTypes, StringComparer.Ordinal);
    }

    /// <summary>
    /// And it decodes bodies rather than reading none.
    /// </summary>
    [Fact]
    public void TheWalkReadsInstructionsRatherThanNone()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnTheResolutionPath);

        Assert.True(reached.MethodsRead > 0);
        Assert.NotEmpty(reached.Members);
    }

    /// <summary>
    /// The bite, executed rather than argued. A fixture entry in this suite
    /// reaches the filename read one type away and names it nowhere itself.
    /// </summary>
    [Fact]
    public void TheWalkFindsAFilenameReadThroughAHelper()
    {
        var reached = AssemblyReachability.From(
            typeof(ResolutionPathTests).Assembly,
            name => string.Equals(name, typeof(ReachabilityEntryThatReadsAFilename).FullName, StringComparison.Ordinal));

        Assert.Contains("System.IO.Path", reached.TypesAmong(FileSystemTypes), StringComparer.Ordinal);
        Assert.Contains("MediaBrowser.Controller.Entities.BaseItem.get_Path", reached.MembersAmong(FileSystemMembers), StringComparer.Ordinal);
    }

    /// <summary>
    /// And the leg that says this is not the lint under another name. The same
    /// assembly reads a filename, and an entry that cannot reach that read is
    /// not refused for it.
    /// </summary>
    [Fact]
    public void TheWalkDoesNotRefuseAReadItCannotReach()
    {
        var assembly = typeof(ResolutionPathTests).Assembly;
        var reached = AssemblyReachability.From(
            assembly,
            name => string.Equals(name, typeof(ReachabilityQuietEntry).FullName, StringComparison.Ordinal));

        Assert.Contains("MediaBrowser.Controller.Entities.BaseItem.get_Path", AssemblyMetadata.MemberNames(assembly), StringComparer.Ordinal);
        Assert.Empty(reached.MembersAmong(FileSystemMembers));
        Assert.Empty(reached.TypesAmong(FileSystemTypes));
    }

    /// <summary>
    /// Every member and every type in the two sets is one that exists. A
    /// vocabulary that has drifted from the runtime or from the server cannot
    /// fire, and it reads exactly like one that is passing.
    /// </summary>
    [Fact]
    public void EveryNameInTheSetsIsOneThatExists()
    {
        var unresolvedMembers = FileSystemMembers.Where(name => !HasMember(name)).ToList();
        var unresolvedTypes = FileSystemTypes.Where(name => Resolve(name) is null).ToList();

        Assert.Empty(unresolvedMembers);
        Assert.Empty(unresolvedTypes);
    }

    private static bool OnTheResolutionPath(string name)
    {
        return ResolutionPaths.Any(path => name.StartsWith(path, StringComparison.Ordinal));
    }

    private static bool HasMember(string qualified)
    {
        var split = qualified.LastIndexOf('.');
        var type = Resolve(qualified[..split]);

        return type is not null && type.GetMember(qualified[(split + 1)..], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Length > 0;
    }

    /// <summary>
    /// Looks a type up in the assemblies that could declare one of these names.
    /// The set spans the runtime, the cryptography assembly and both server
    /// packages, so one lookup would find part of the vocabulary and report the
    /// rest as drift.
    /// </summary>
    /// <param name="full">The type's full name.</param>
    /// <returns>The type, or null where none of them has it.</returns>
    private static Type? Resolve(string full)
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(System.IO.Path).Assembly,
            typeof(System.Security.Cryptography.MD5).Assembly,
            typeof(BaseItem).Assembly,
            typeof(MediaBrowser.Model.Entities.ImageType).Assembly,
        };

        return assemblies.Select(assembly => assembly.GetType(full, throwOnError: false)).FirstOrDefault(found => found is not null);
    }
}
