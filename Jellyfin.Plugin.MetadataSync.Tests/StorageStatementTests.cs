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
/// not carry is red too. The document's negative claim, that nothing writes to a
/// disk, is re-derived over the plugin's sources rather than read.
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
    /// The command the section pastes an empty result under.
    /// </summary>
    private const string WriteCommand = "    git grep -In \"FileStream|StreamWriter|File.Write|File.Create|File.AppendAll\" -- 'Jellyfin.Plugin.MetadataSync/'";

    /// <summary>
    /// What the section pastes under a command whose result is empty.
    /// </summary>
    private const string NoOutput = "    # no output, exit 1";

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
    /// The claim that nothing writes to a disk is re-derived rather than read.
    /// The section pastes the command with an empty result under it, and both
    /// the paste and the emptiness are checked, so a write that arrives without
    /// the paragraph being rewritten reddens here.
    /// </summary>
    [Fact]
    public void TheClaimThatNothingWritesToADiskIsStillTrue()
    {
        var lines = Lines(_document);
        var head = lines.IndexOf(WriteCommand);

        Assert.True(head >= 0, "The section no longer carries the command the absence of a write rests on.");
        Assert.Equal(NoOutput, lines[head + 1]);

        var writing = SourceFiles()
            .Where(path =>
            {
                var text = File.ReadAllText(path);

                return _writes.Any(call => text.Contains(call, StringComparison.Ordinal));
            })
            .Select(Relative)
            .ToList();

        Assert.Empty(writing);
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
    private static List<string> Tables()
    {
        var lines = Lines(_document);
        var opens = lines.IndexOf(ListOpens);
        var closes = lines.IndexOf(ListCloses);

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
