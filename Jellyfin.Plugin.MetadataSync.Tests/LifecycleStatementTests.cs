using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the closing section of <c>docs/lifecycle.md</c> against the tree it
/// describes.
///
/// That section is where the three acts stop being decisions and start being
/// claims about what is here. It said there was no store and that nothing in
/// this plugin wrote anything to a disk, #16 built one, and both sentences
/// stopped being true on that merge. Nothing reported it.
/// <c>docs/storage.md</c> carried the same claim, is read by
/// <see cref="StorageStatementTests"/>, and reddened on the change that
/// falsified it; this page was copied to no test output and went on describing
/// the tree from before the store until somebody grepped the documents by hand.
/// The interval between a sentence going stale and somebody meeting it was
/// therefore bounded by nothing.
///
/// What it reads, in both directions each time. The sources that write to a
/// disk, so a second file that writes with no line on the page is red and a
/// line naming a file that has stopped writing is red too. The members the
/// plugin overrides on the server's plugin base, which is the set the page's
/// sentence about the uninstall hook is a statement about: #62's own third
/// condition is what would add <c>OnUninstalling</c>, and the page has to move
/// when it does. And the paths the page says the tree does not carry, so the
/// day <c>docs/consumer.md</c> arrives the page reds instead of going on saying
/// it is missing.
///
/// The last of those three is a negative disclosure and stays one. What is
/// asserted is the absence the page states, never that the absence is harmless,
/// and a path that has arrived is a red suite rather than a line quietly
/// deleted from the list.
///
/// What it cannot reach, stated rather than left to be assumed. It judges the
/// three lists and never the prose around them, so a claim written in a
/// sentence with no list under it is unread, and a list entry describing its
/// subject wrongly passes. The write reading is by the spelling of a call, the
/// same bound <see cref="StorageStatementTests"/> states for its own, so a
/// write reached through a type these names do not spell is outside it. The
/// override reading is by the text of the plugin's own source rather than by
/// the runtime type, so a member overridden in a partial declaration this file
/// does not carry is outside it. And the absent-path reading resolves against
/// the documents this run copied rather than against the mainline.
/// </summary>
public class LifecycleStatementTests
{
    /// <summary>
    /// The comment that opens the fence around the sources that write.
    /// </summary>
    private const string WritersOpen = "<!-- the plugin sources that write to a disk: one per line, the file first, read by LifecycleStatementTests -->";

    /// <summary>
    /// The comment that closes it.
    /// </summary>
    private const string WritersClose = "<!-- end of the sources that write -->";

    /// <summary>
    /// The comment that opens the fence around the members the plugin
    /// overrides.
    /// </summary>
    private const string OverridesOpen = "<!-- the members Jellyfin.Plugin.MetadataSync/Plugin.cs overrides: one per line, the member first, read by LifecycleStatementTests -->";

    /// <summary>
    /// The comment that closes it.
    /// </summary>
    private const string OverridesClose = "<!-- end of the members overridden -->";

    /// <summary>
    /// The comment that opens the fence around the paths the page says are not
    /// here.
    /// </summary>
    private const string AbsentOpen = "<!-- the paths this page says the tree does not carry: one per line, the path first, read by LifecycleStatementTests -->";

    /// <summary>
    /// The comment that closes it.
    /// </summary>
    private const string AbsentClose = "<!-- end of the paths the tree does not carry -->";

    /// <summary>
    /// The document, copied to the output for the reason every other document
    /// read here is: walking up from the test binary answers a different
    /// question on a machine where the tests run from somewhere else.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "lifecycle.md");

    /// <summary>
    /// The plugin's sources, copied beside the test binary by the test project
    /// file.
    /// </summary>
    private static readonly string _sources = Path.Combine(AppContext.BaseDirectory, "plugin-sources");

    /// <summary>
    /// The repository's documents, copied whole beside the test binary by the
    /// test project file, which is what an absent path is resolved against. It
    /// is the directory rather than the documents this suite copies by name,
    /// because a path resolved against those would answer no for any file
    /// nobody had added a copy line for, and the leg would then pass on the one
    /// day it exists to fail on.
    /// </summary>
    private static readonly string _documents = Path.Combine(AppContext.BaseDirectory, "documents");

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
    /// The sources the page names as writing to a disk are the sources that
    /// write to one, as a set and in both directions. This is the claim that
    /// went stale, and holding it here rather than only on
    /// <c>docs/storage.md</c> is the difference between one page moving on the
    /// change that falsifies both and both moving.
    /// </summary>
    [Fact]
    public void TheSourcesThePageNamesAreTheSourcesThatWriteToADisk()
    {
        Assert.Equal(
            Writing().OrderBy(path => path, StringComparer.Ordinal).ToList(),
            Declared(WritersOpen, WritersClose).OrderBy(path => path, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The members the page says this plugin takes over from the server's
    /// plugin base are the members it takes over, as a set and in both
    /// directions. The page's sentence about the uninstall hook is a claim
    /// about this set rather than about one name, so an override arriving reds
    /// the page whichever member it is.
    /// </summary>
    [Fact]
    public void TheMembersThePageNamesAreTheMembersThePluginOverrides()
    {
        Assert.Equal(
            Overridden().OrderBy(member => member, StringComparer.Ordinal).ToList(),
            Declared(OverridesOpen, OverridesClose).OrderBy(member => member, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// Every path the page says the tree does not carry is a path the tree does
    /// not carry. The assertion is the absence the page states and nothing
    /// more: a path that has arrived is a failure naming it, so the sentence
    /// above the list is rewritten by whoever lands the thing rather than by
    /// whoever next reads the page.
    /// </summary>
    [Fact]
    public void EveryPathThePageSaysIsNotHereIsNotHere()
    {
        var arrived = Declared(AbsentOpen, AbsentClose)
            .Where(Here)
            .ToList();

        Assert.Empty(arrived);
    }

    /// <summary>
    /// Each fence is still there and the list inside it is not empty. Without
    /// this leg a renamed comment, or a list somebody emptied, would leave the
    /// comparisons above failing for a reason nobody could place, and the
    /// absent-path leg would pass over an empty read rather than assert
    /// anything at all.
    /// </summary>
    /// <param name="opensWith">The comment that opens the fence.</param>
    /// <param name="closesWith">The comment that closes it.</param>
    [Theory]
    [InlineData(WritersOpen, WritersClose)]
    [InlineData(OverridesOpen, OverridesClose)]
    [InlineData(AbsentOpen, AbsentClose)]
    public void TheFenceTheComparisonReadsIsStillThere(string opensWith, string closesWith)
    {
        var lines = Lines(_document);

        Assert.Contains(opensWith, lines, StringComparer.Ordinal);
        Assert.Contains(closesWith, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Declared(opensWith, closesWith));
    }

    /// <summary>
    /// The absent-path leg reads the documents it judges rather than a
    /// directory that is not there, which is what would make it pass for the
    /// wrong reason. Two documents nobody is claiming are absent resolve by the
    /// same route the leg uses, one of them a page this suite has no copy line
    /// for, so what the leg reads is the directory rather than the handful of
    /// files copied by name.
    /// </summary>
    /// <param name="path">A document that is in the tree.</param>
    [Theory]
    [InlineData("docs/storage.md")]
    [InlineData("docs/direction.md")]
    public void TheAbsentPathLegResolvesADocumentThatIsHere(string path)
    {
        Assert.True(Here(path));
    }

    /// <summary>
    /// Whether a path the page names is in the tree this run was built from.
    /// The documents are copied under one directory keeping their own layout,
    /// so the path the page writes is used from <c>docs/</c> down.
    /// </summary>
    /// <param name="path">The path as the document writes it.</param>
    /// <returns>Whether it is here.</returns>
    private static bool Here(string path) =>
        File.Exists(DeclaredPath.Resolve(
            "docs/lifecycle.md",
            _documents,
            path.Replace("docs/", string.Empty, StringComparison.Ordinal)));

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
    /// The members <c>Plugin.cs</c> declares with the override keyword, read
    /// off the copied source. The name is the word two places after the
    /// keyword, which is past the return type, and an opening brace, a fat
    /// arrow or an argument list is cut away first so a property and a method
    /// are read the same way.
    /// </summary>
    /// <returns>The member names.</returns>
    private static List<string> Overridden()
    {
        var file = Path.Combine(_sources, "Plugin.cs");

        if (!File.Exists(file))
        {
            return new List<string>();
        }

        var members = new List<string>();

        foreach (var line in Lines(file))
        {
            var trimmed = line.Trim();

            if (!trimmed.Contains(" override ", StringComparison.Ordinal))
            {
                continue;
            }

            var words = trimmed
                .Replace("=>", " ", StringComparison.Ordinal)
                .Replace("{", " ", StringComparison.Ordinal)
                .Replace("(", " ", StringComparison.Ordinal)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var name = words
                .SkipWhile(word => !string.Equals(word, "override", StringComparison.Ordinal))
                .Skip(2)
                .FirstOrDefault();

            if (name is not null)
            {
                members.Add(name);
            }
        }

        return members;
    }

    /// <summary>
    /// The entries a fenced list in the document declares. One reading rather
    /// than one per fence, so the three cannot drift into being read by three
    /// copies of this that differ in a way nobody notices.
    /// </summary>
    /// <param name="opensWith">The comment that opens the fence.</param>
    /// <param name="closesWith">The comment that closes it.</param>
    /// <returns>The entries, as the document quotes them.</returns>
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
        var entries = new List<string>();

        foreach (var line in lines.Skip(opens + 1).Take(closes - opens - 1))
        {
            if (!line.StartsWith(head, StringComparison.Ordinal))
            {
                continue;
            }

            var end = line.IndexOf(Quote, head.Length, StringComparison.Ordinal);

            if (end > head.Length)
            {
                entries.Add(line[head.Length..end]);
            }
        }

        return entries;
    }

    /// <summary>
    /// The character an entry is quoted with in the document, as its own
    /// constant so the reading above stays legible.
    /// </summary>
    private static string Quote => "`";

    /// <summary>
    /// A copied source read back as the document names it, with one separator
    /// whichever platform it ran on.
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
