using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.MetadataSync.Store;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the readme's uninstall section against the files the stores keep.
///
/// That section answers the question an operator asks before removing a plugin,
/// which is what stays on their disk afterwards. It answered it with a negative:
/// the records did not exist, the store they would live in was not built, and
/// there was nothing on disk to leave behind. All three stopped being true when
/// the store landed, and the sentence promising that this section would say so
/// when they did was the one thing nobody ran.
///
/// The two documents carrying the same claim reddened on the change that
/// falsified it, because <see cref="StorageStatementTests"/> and
/// <see cref="LifecycleStatementTests"/> read them. Nothing read the readme, so
/// the file a reader meets before deciding whether to open any of the others
/// went on describing a tree from before the change. This is that reading, and
/// it is the same shape as the other two rather than a new idea: a fenced list
/// in the document, a derived list from the assembly, and a comparison in both
/// directions.
///
/// What it reads. The file names inside the first fence are compared with the
/// names the stores themselves declare, so a store added with no line in the
/// readme is red and a line naming a file no store keeps is red too. The areas
/// inside the second are compared with the directories the plugin's sources are
/// in, the same way, and that fence exists because the paragraph it sits under
/// named five parts of this plugin and left out the rest - the stores, the two
/// resolvers and the migration chain all arrived under a sentence that went on
/// listing what somebody had remembered.
///
/// What it cannot reach, stated rather than left to be assumed. It judges the
/// names and never the sentence beside each one, so a file described wrongly
/// passes. It reads one fence rather than the file, so every other claim the
/// readme makes about the tree is held by a reader and by nothing else. And it
/// says nothing about whether an uninstall actually leaves these files: what
/// happens to the store on an uninstall is the hook the plugin does not
/// override, which <see cref="LifecycleStatementTests"/> holds. That a store
/// keeping a file declares its name at all is held by
/// <see cref="StorageStatementTests"/> and is not restated here, because a
/// second copy of that leg would be the drift this file exists against.
/// </summary>
public class ReadmeStatementTests
{
    /// <summary>
    /// The comment that opens the fence around the list of files left behind.
    /// </summary>
    private const string ListOpens = "<!-- the files this plugin leaves in its data folder: one per line, the file first, read by ReadmeStatementTests -->";

    /// <summary>
    /// The comment that closes the fence.
    /// </summary>
    private const string ListCloses = "<!-- end of the files left behind -->";

    /// <summary>
    /// The comment that opens the fence around the list of areas this plugin is
    /// built out of.
    /// </summary>
    private const string AreasOpen = "<!-- the areas this plugin is built out of: one per line, the directory first, read by ReadmeStatementTests -->";

    /// <summary>
    /// The comment that closes that fence.
    /// </summary>
    private const string AreasClose = "<!-- end of the areas -->";

    /// <summary>
    /// The readme, copied beside the test binary rather than found by walking up
    /// from it, so the suite reads the same bytes wherever it runs.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "README.md");

    /// <summary>
    /// The plugin's sources, copied beside the test binary by the test project
    /// file, which is the same set every other reading of them here uses.
    /// </summary>
    private static readonly string _sources = Path.Combine(AppContext.BaseDirectory, "plugin-sources");

    /// <summary>
    /// The files the readme says are left behind are the files the stores keep,
    /// in both directions.
    /// </summary>
    [Fact]
    public void TheFilesTheReadmeNamesAreTheFilesTheStoresKeep()
    {
        Assert.Equal(
            StoreFileNames().OrderBy(name => name, StringComparer.Ordinal).ToList(),
            Declared().OrderBy(name => name, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The fence is still there and neither side of the comparison is empty.
    /// Without this leg a renamed comment, or a list somebody emptied, leaves the
    /// comparison above passing on two empty answers that agree, which is the
    /// one way it can report success without having looked.
    /// </summary>
    [Fact]
    public void TheListTheComparisonReadsIsStillThere()
    {
        var lines = Lines();

        Assert.Contains(ListOpens, lines, StringComparer.Ordinal);
        Assert.Contains(ListCloses, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Declared());
        Assert.NotEmpty(StoreFileNames());
    }

    /// <summary>
    /// The areas the readme names are the directories the plugin's sources are
    /// in, as a set and in both directions.
    /// </summary>
    /// <remarks>
    /// The paragraph this list sits under named five parts and left out the
    /// rest, and it read that way while the stores, the two resolvers and the
    /// migration chain arrived under it. It is the first thing in the file after
    /// the sentence saying there is no release, so a reader deciding whether any
    /// of this exists was told about a third of it.
    /// <para>
    /// A directory is the unit rather than a type or a file because that is what
    /// the paragraph is about and because it is the coarsest thing that cannot
    /// be added by accident: a new area is somebody deciding this plugin does a
    /// new kind of work, which is exactly the moment this paragraph has to be
    /// read again.
    /// </para>
    /// <para>
    /// This is not a second copy of the folder accounting
    /// <see cref="SecurityPolicyTests"/> makes, and the difference is worth
    /// stating because a new folder reds both. That one asks whether every
    /// folder holds a part <c>SECURITY.md</c> names or is declared not to be
    /// one, which is a question about decisions somebody has to review. This
    /// asks whether the readme's opening paragraph names the areas the plugin
    /// has, which is a question about what a reader is told exists. Both are
    /// derived from the same tree in both directions, so neither can drift
    /// against it in silence, and that is what separates this from the two
    /// writable-field sets #233 measured: those were each green against a
    /// different authority.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheAreasTheReadmeNamesAreTheDirectoriesThePluginIsBuiltOutOf()
    {
        Assert.Equal(
            Areas().OrderBy(name => name, StringComparer.Ordinal).ToList(),
            Declared(AreasOpen, AreasClose).OrderBy(name => name, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// That fence is still there and neither side of its comparison is empty,
    /// for the reason the fence above has such a leg: two empty answers agree,
    /// and a run that found no sources at all would be the one way this reports
    /// success without having looked.
    /// </summary>
    [Fact]
    public void TheListOfAreasIsStillThere()
    {
        var lines = Lines();

        Assert.Contains(AreasOpen, lines, StringComparer.Ordinal);
        Assert.Contains(AreasClose, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Declared(AreasOpen, AreasClose));
        Assert.NotEmpty(Areas());
    }

    /// <summary>
    /// The directories the plugin's own sources are in. A source at the root of
    /// the plugin is in no area and is left out: the entry point and the
    /// registrator are how the server reaches this plugin rather than a kind of
    /// work it does, and the paragraph is about the second.
    /// </summary>
    /// <returns>The directory names.</returns>
    private static List<string> Areas() =>
        Directory.Exists(_sources)
            ? Directory.EnumerateFiles(_sources, "*.cs", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(_sources, path).Replace(Path.DirectorySeparatorChar, '/'))
                .Where(path => path.Contains('/', StringComparison.Ordinal))
                .Select(path => path[..path.IndexOf('/', StringComparison.Ordinal)])
                .Distinct(StringComparer.Ordinal)
                .ToList()
            : new List<string>();

    /// <summary>
    /// The stores this plugin declares, found rather than listed, which is the
    /// same reading <see cref="StorageStatementTests"/> makes for its own list.
    /// </summary>
    /// <returns>The concrete types implementing the store shape.</returns>
    private static IEnumerable<Type> StoreTypes() =>
        typeof(Plugin).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IPairingStore).IsAssignableFrom(type));

    /// <summary>
    /// The file names the stores declare.
    /// </summary>
    /// <returns>The names.</returns>
    private static List<string> StoreFileNames() =>
        StoreTypes()
            .Select(type => type.GetField("FileName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetRawConstantValue() as string)
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The file names the fenced list in the readme declares.
    /// </summary>
    /// <returns>The names, as the document spells them.</returns>
    private static List<string> Declared() => Declared(ListOpens, ListCloses);

    /// <summary>
    /// The names a fenced list in the readme declares. One reading rather than
    /// one per fence, so a second list cannot drift into being read by a copy of
    /// this that differs from it in a way nobody notices.
    /// </summary>
    /// <param name="opensWith">The comment that opens the fence.</param>
    /// <param name="closesWith">The comment that closes it.</param>
    /// <returns>The names, as the document spells them.</returns>
    private static List<string> Declared(string opensWith, string closesWith)
    {
        var lines = Lines();
        var opens = lines.IndexOf(opensWith);
        var closes = lines.IndexOf(closesWith);

        if (opens < 0 || closes < opens)
        {
            return new List<string>();
        }

        var head = "- " + Quote;
        var names = new List<string>();

        foreach (var line in lines.Skip(opens + 1).Take(closes - opens - 1))
        {
            if (!line.StartsWith(head, StringComparison.Ordinal))
            {
                continue;
            }

            var end = line.IndexOf(Quote, head.Length, StringComparison.Ordinal);

            if (end > head.Length)
            {
                names.Add(line[head.Length..end]);
            }
        }

        return names;
    }

    /// <summary>
    /// The character a file name is quoted with in the document, as its own
    /// constant so the reading above stays legible.
    /// </summary>
    private static string Quote => "`";

    /// <summary>
    /// The document's lines, trimmed at the end so a trailing carriage return
    /// cannot make a fence comment fail to match itself.
    /// </summary>
    /// <returns>The lines.</returns>
    private static List<string> Lines() =>
        File.ReadAllLines(_document)
            .Select(line => line.TrimEnd())
            .ToList();
}
