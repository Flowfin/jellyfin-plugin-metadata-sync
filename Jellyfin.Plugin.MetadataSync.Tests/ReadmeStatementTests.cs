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
/// What it reads. The file names inside the fence are compared with the names
/// the stores themselves declare, so a store added with no line in the readme is
/// red and a line naming a file no store keeps is red too.
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
    /// The readme, copied beside the test binary rather than found by walking up
    /// from it, so the suite reads the same bytes wherever it runs.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "README.md");

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
    private static List<string> Declared()
    {
        var lines = Lines();
        var opens = lines.IndexOf(ListOpens);
        var closes = lines.IndexOf(ListCloses);

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
