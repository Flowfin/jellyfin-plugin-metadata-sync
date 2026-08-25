using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the closing section of <c>docs/storage.md</c> against what the plugin
/// project declares and against the plugin's own sources.
///
/// That section is the argument for the table above it. It tells a reader that
/// everything this plugin reads is embedded in the assembly and read rather
/// than kept, so an absent store is a property of the plugin and not an
/// omission. It counted two tables while the project declared four, because the
/// conflict rules and the reference comparison arrived after the sentence was
/// written and nobody re-ran it. A count that is half the real one weakens
/// exactly the argument it is there to make, in the file somebody reads to
/// decide what this plugin keeps about their library.
///
/// It is the sixth document here found describing a tree that had moved under
/// it, and every one was found by somebody reading it while working on
/// something else, so the interval between a sentence going stale and somebody
/// meeting it was bounded by nothing.
///
/// What it reads. The list of tables is compared with the project's declared
/// embedded resources in both directions, so a table added to the assembly with
/// no line in the document is red and a line naming a table the assembly does
/// not carry is red too. The list of sources that write to a disk is compared
/// with the plugin's own sources the same way, and it replaced a negative claim
/// that nothing wrote at all: #16 built the store, so the section names what
/// writes instead of pasting an empty result, and both directions are held. A
/// second file that writes with no line in the document is red, and a line
/// naming a file that has stopped writing is red too.
///
/// Existence of a named file is not checked here and does not need to be: an
/// <c>EmbeddedResource</c> naming a file that is not in the tree fails the
/// build, so the comparison above is already against files that exist.
///
/// What it cannot reach, stated rather than left to be assumed. It judges the
/// paths and never the sentence beside each one, so a table described wrongly
/// passes. It reads one section rather than the file. And the absence of a write
/// is judged by the spelling of a call, so a write reached through a type this
/// list does not name is outside it, which is the same bound
/// <c>docs/personal-data.md</c> states for its own token reading.
/// </summary>
public class StorageStatementTests
{
    /// <summary>
    /// The comment that opens the fence around the list of tables.
    /// </summary>
    private const string ListOpens = "<!-- the tables embedded in the assembly: one per line, the file first, read by StorageStatementTests -->";

    /// <summary>
    /// The comment that closes the fence.
    /// </summary>
    private const string ListCloses = "<!-- end of the tables -->";

    /// <summary>
    /// The comment that opens the fence around the list of sources that write.
    /// </summary>
    private const string WritersOpen = "<!-- the plugin sources that write to a disk: one per line, the file first, read by StorageStatementTests -->";

    /// <summary>
    /// The comment that closes that fence.
    /// </summary>
    private const string WritersClose = "<!-- end of the sources that write -->";

    /// <summary>
    /// The document, copied to the output for the reason the field register is:
    /// walking up from the test binary answers a different question on a machine
    /// where the tests run from somewhere else.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "storage.md");

    /// <summary>
    /// The plugin's sources, copied beside the test binary by the test project
    /// file.
    /// </summary>
    private static readonly string _sources = Path.Combine(AppContext.BaseDirectory, "plugin-sources");

    /// <summary>
    /// The calls the pasted command matches on, spelled as it spells them.
    /// </summary>
    private static readonly string[] _writes =
    {
        "FileStream",
        "StreamWriter",
        "File.Write",
        "File.Create",
        "File.AppendAll",
    };

    /// <summary>
    /// The tables the document names are the tables the assembly carries, as a
    /// set and in both directions. One direction catches a table added to the
    /// assembly that the document does not mention, which is the direction this
    /// defect took. The other catches a line left behind by a table that moved.
    /// </summary>
    [Fact]
    public void TheTablesTheSectionNamesAreTheTablesTheAssemblyCarries()
    {
        Assert.Equal(
            PluginProjectFile.EmbeddedTables().OrderBy(path => path, StringComparer.Ordinal).ToList(),
            Tables().OrderBy(path => path, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The sources the section names as writing to a disk are the sources that
    /// write to one, as a set and in both directions. This stood as a claim that
    /// nothing wrote at all, pasted as a command with an empty result under it,
    /// and #16 built the store the claim was true because of. What replaced it
    /// is not a weaker guard: an empty result was one comparison and a named
    /// list is two, so a second file that writes to a disk is now caught by the
    /// same leg that used to catch the first.
    /// </summary>
    [Fact]
    public void TheSourcesTheSectionNamesAreTheSourcesThatWriteToADisk()
    {
        Assert.Equal(
            Writing().OrderBy(path => path, StringComparer.Ordinal).ToList(),
            Declared(WritersOpen, WritersClose).OrderBy(path => path, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The fence around that list is still there and the list inside it is not
    /// empty, for the same reason the tables have such a leg: a renamed comment
    /// would leave the comparison above failing where nobody could place it.
    /// </summary>
    [Fact]
    public void TheListOfSourcesThatWriteIsStillThere()
    {
        var lines = Lines(_document);

        Assert.Contains(WritersOpen, lines, StringComparer.Ordinal);
        Assert.Contains(WritersClose, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Declared(WritersOpen, WritersClose));
    }

    /// <summary>
    /// The plugin sources that name a call that writes to a disk.
    /// </summary>
    /// <returns>The paths, relative to the repository root.</returns>
    private static List<string> Writing()
    {
        return SourceFiles()
            .Where(path =>
            {
                var text = File.ReadAllText(path);

                return _writes.Any(call => text.Contains(call, StringComparison.Ordinal));
            })
            .Select(Relative)
            .ToList();
    }

    /// <summary>
    /// The fence is still there and the list inside it is not empty. Without
    /// this leg a renamed comment, or a list somebody emptied, would leave the
    /// comparison above failing for a reason nobody could place, and a reader of
    /// that failure would look at the project rather than at the fence.
    /// </summary>
    [Fact]
    public void TheListTheComparisonReadsIsStillThere()
    {
        var lines = Lines(_document);

        Assert.Contains(ListOpens, lines, StringComparer.Ordinal);
        Assert.Contains(ListCloses, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Tables());
    }

    /// <summary>
    /// The paths the fenced list declares.
    /// </summary>
    /// <returns>The paths, relative to the repository root.</returns>
    private static List<string> Tables() => Declared(ListOpens, ListCloses);

    /// <summary>
    /// The paths a fenced list in the document declares. One reading rather than
    /// one per fence, so the second list cannot drift into being read by a
    /// second copy of this that differs from it in a way nobody notices.
    /// </summary>
    /// <param name="opensWith">The comment that opens the fence.</param>
    /// <param name="closesWith">The comment that closes it.</param>
    /// <returns>The paths, relative to the repository root.</returns>
    private static List<string> Declared(string opensWith, string closesWith)
    {
        var lines = Lines(_document);
        var opens = lines.IndexOf(opensWith);
        var closes = lines.IndexOf(closesWith);

        if (opens < 0 || closes < opens)
        {
            return new List<string>();
        }

        var head = "- " + Quote;
        var paths = new List<string>();

        foreach (var line in lines.Skip(opens + 1).Take(closes - opens - 1))
        {
            if (!line.StartsWith(head, StringComparison.Ordinal))
            {
                continue;
            }

            var end = line.IndexOf(Quote, head.Length, StringComparison.Ordinal);

            if (end > head.Length)
            {
                paths.Add(line[head.Length..end]);
            }
        }

        return paths;
    }

    /// <summary>
    /// The character a path is quoted with in the document, as its own constant
    /// so the reading above stays legible.
    /// </summary>
    private static string Quote => "`";

    /// <summary>
    /// A copied source read back as the document would name it, with one
    /// separator whichever platform it ran on.
    /// </summary>
    /// <param name="path">The copied file.</param>
    /// <returns>The path relative to the repository root.</returns>
    private static string Relative(string path) =>
        "Jellyfin.Plugin.MetadataSync/" + Path.GetRelativePath(_sources, path).Replace('\\', '/');

    /// <summary>
    /// The plugin's own sources, copied beside the test binary by the test
    /// project file. Reading them from a path relative to the source tree would
    /// work on a developer's machine and not in a packaging job.
    /// </summary>
    /// <returns>The files.</returns>
    private static IReadOnlyList<string> SourceFiles() =>
        Directory.Exists(_sources)
            ? Directory.EnumerateFiles(_sources, "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList()
            : Array.Empty<string>();

    /// <summary>
    /// A file's lines, with the line ending normalised so the reading is the
    /// same on either platform and each line trimmed of trailing space, so an
    /// invisible byte is not the difference between a match and a failure.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The lines.</returns>
    private static List<string> Lines(string path) =>
        File.ReadAllText(path)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();
}
