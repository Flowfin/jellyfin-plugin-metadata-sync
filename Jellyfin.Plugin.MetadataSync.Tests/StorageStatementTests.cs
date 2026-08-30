using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.MetadataSync.Store;
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
/// naming a file that has stopped writing is red too. The list of sources that
/// build the migration chain is compared the same way, and it is the positive
/// half of a claim two pages carried the negative of after the chain existed.
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
    /// The comment that opens the fence around the list of sources that build
    /// the migration chain.
    /// </summary>
    private const string ChainOpens = "<!-- the plugin sources that build the migration chain: one per line, the file first, read by StorageStatementTests -->";

    /// <summary>
    /// The comment that closes that fence.
    /// </summary>
    private const string ChainCloses = "<!-- end of the sources that build the chain -->";

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
    /// The type a source has to name to be building the migration chain, spelled
    /// as the document's list is about. One name rather than several, because a
    /// step is the only thing a chain is assembled out of: a source that moves a
    /// store forward without naming it is not a step, and the chain cannot take
    /// it.
    /// </summary>
    private const string Step = "FormatStep";

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
    /// The sources the section names as building the migration chain are the
    /// sources that build it, as a set and in both directions.
    /// </summary>
    /// <remarks>
    /// This is the positive half of a claim two pages carried the negative of
    /// after it had stopped being true. The chain landed under #59, and the
    /// closing section of this document and <c>docs/lifecycle.md</c> both went
    /// on saying it was not built, each a second copy of the paragraph this
    /// list now sits under. Both copies are deleted and point here instead, and
    /// what this leg adds is that the paragraph they point at cannot itself go
    /// stale in silence: a third source joining the chain reddens it, and so
    /// does the last one leaving.
    /// <para>
    /// What it cannot reach. It judges which files name a step and never
    /// whether the chain those files assemble is correct, which is what
    /// <see cref="StoreMigrationTests"/> is for, and it reads a spelling rather
    /// than a call, so a source reaching the chain through a name this reading
    /// does not carry is outside it. That is the same bound the two lists above
    /// state for their own readings.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheSourcesTheSectionNamesAreTheSourcesThatBuildTheChain()
    {
        Assert.Equal(
            BuildingTheChain().OrderBy(path => path, StringComparer.Ordinal).ToList(),
            Declared(ChainOpens, ChainCloses).OrderBy(path => path, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The fence around that list is still there and the list inside it is not
    /// empty, for the reason the other two fences have such a leg: a renamed
    /// comment would leave the comparison above failing where nobody could place
    /// it, and an empty list would agree with a tree that had lost the chain.
    /// </summary>
    [Fact]
    public void TheListOfSourcesThatBuildTheChainIsStillThere()
    {
        var lines = Lines(_document);

        Assert.Contains(ChainOpens, lines, StringComparer.Ordinal);
        Assert.Contains(ChainCloses, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Declared(ChainOpens, ChainCloses));
    }

    /// <summary>
    /// The store files the document names are the files the stores actually
    /// keep, as a set and in both directions.
    /// </summary>
    /// <remarks>
    /// The two lists above are held and the names inside the prose beside them
    /// were not. `written-values.jsonl` has been in this document since #16 and
    /// `store-format.json` arrived with #59, and until this leg a store that
    /// renamed its file left both sentences describing a file that is not there,
    /// with every route in this repository green. That is the residual #87
    /// records for the prose around a rendered table, met here in the one form
    /// where it is decidable rather than a judgement: a file name is a literal
    /// in a document and a literal in a type, and the two either agree or they
    /// do not.
    /// <para>
    /// The document's side is derived rather than listed. An inline-code span
    /// naming no directory is a bare file name, and every one of them in this
    /// document is a file a store keeps; the paths in the two fenced lists carry
    /// a directory and are outside it by that alone.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheStoreFilesTheDocumentNamesAreTheFilesTheStoresKeep()
    {
        Assert.Equal(
            StoreFileNames().OrderBy(name => name, StringComparer.Ordinal).ToList(),
            BareFileNamesInTheProse().OrderBy(name => name, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// Every store that keeps a file declares its name in one place, and the two
    /// sides of the comparison above are not empty.
    /// </summary>
    /// <remarks>
    /// Without the first half the comparison passes for the wrong reason the day
    /// a store keeps a file under a name spelled at the site that opens it: the
    /// reflection finds no constant, the document names nothing new, and the two
    /// empty answers agree. A store that persists is already required to be one
    /// of these types, so what is added here is that it says which file it is.
    /// </remarks>
    [Fact]
    public void EveryStoreThatKeepsAFileSaysWhichFileItIs()
    {
        var persisting = Writing()
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToList();

        var declaring = StoreTypes()
            .Where(type => FileNameOf(type) is not null)
            .Select(type => type.Name)
            .ToList();

        Assert.NotEmpty(persisting);
        Assert.Empty(persisting.Where(name => !declaring.Contains(name, StringComparer.Ordinal)).ToList());
        Assert.NotEmpty(StoreFileNames());
        Assert.NotEmpty(BareFileNamesInTheProse());
    }

    /// <summary>
    /// The stores this plugin declares, found rather than listed, which is the
    /// same reading <see cref="PairingStoresTests"/> makes for the report.
    /// </summary>
    /// <returns>The concrete types implementing the store shape.</returns>
    private static IEnumerable<Type> StoreTypes() =>
        typeof(Plugin).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IPairingStore).IsAssignableFrom(type));

    /// <summary>
    /// The file name a store declares, or null where it keeps no file.
    /// </summary>
    /// <param name="type">The store type.</param>
    /// <returns>The declared name, or null.</returns>
    private static string? FileNameOf(Type type) =>
        type.GetField("FileName", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetRawConstantValue() as string;

    /// <summary>
    /// The file names the stores declare.
    /// </summary>
    /// <returns>The names.</returns>
    private static List<string> StoreFileNames() =>
        StoreTypes()
            .Select(FileNameOf)
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The bare file names the document's prose carries in inline code, which is
    /// every quoted span naming no directory and carrying an extension.
    /// </summary>
    /// <returns>The names.</returns>
    private static List<string> BareFileNamesInTheProse() =>
        Regex.Matches(File.ReadAllText(_document), "`([^`]+)`")
            .Select(match => match.Groups[1].Value)
            .Where(span => !span.Contains('/', StringComparison.Ordinal))
            .Where(span => Regex.IsMatch(span, @"^[A-Za-z0-9._-]+\.[A-Za-z0-9]+$"))
            .Distinct(StringComparer.Ordinal)
            .ToList();

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
    /// The plugin sources that name a migration step, which is what building the
    /// chain is spelled as.
    /// </summary>
    /// <returns>The paths, relative to the repository root.</returns>
    private static List<string> BuildingTheChain()
    {
        return SourceFiles()
            .Where(path => File.ReadAllText(path).Contains(Step, StringComparison.Ordinal))
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
