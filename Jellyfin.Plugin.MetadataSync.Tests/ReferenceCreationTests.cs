using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the paragraph of <c>docs/references.md</c> that says nothing creates a
/// reference against the sources it is a claim about.
///
/// That paragraph said the mark a created entry has to carry had nowhere to be
/// kept, and named the issue that would build the place. The place was built:
/// #47 landed the record of what this plugin wrote and #61 gave every store one
/// shape, and the sentence went on saying the opposite in three files at once,
/// one of them the resolver's own remarks, which is where somebody deciding to
/// build the missing half reads first. Nothing reported it, because nothing
/// read any of the three.
///
/// What it reads, in both directions. The plugin sources that name the resolver
/// are compared with the list the paragraph carries, so a second source naming
/// it is red and a line naming a source that has stopped naming it is red too.
/// The claim in the paragraph is that the only source naming the resolver is
/// the one declaring it, and that is a set rather than a sentence.
///
/// This is a negative disclosure and stays one. What the leg asserts is the
/// absence the paragraph states; it says nothing about the absence being
/// harmless, and a caller arriving is a red suite rather than a line quietly
/// added to the list.
///
/// What it cannot reach, stated rather than left to be assumed. The reading is
/// by the spelling of the type's name in the source text, so a call reached
/// through an alias, through reflection or through a wrapper that this name
/// does not appear in is outside it, which is the same bound
/// <see cref="StorageStatementTests"/> states for its own token reading. It
/// judges the list and never the prose around it, so a sentence with no list
/// under it is unread. And a source naming the resolver only in a comment
/// counts as naming it, which is the direction that fails closed: it asks to be
/// written down rather than passing.
/// </summary>
public class ReferenceCreationTests
{
    /// <summary>
    /// The comment that opens the fence around the sources that name the
    /// resolver.
    /// </summary>
    private const string NamesOpen = "<!-- the plugin sources that name the reference resolver: one per line, the file first, read by ReferenceCreationTests -->";

    /// <summary>
    /// The comment that closes it.
    /// </summary>
    private const string NamesClose = "<!-- end of the sources that name the resolver -->";

    /// <summary>
    /// The comment that opens the fence around the sources that name a
    /// reference outcome.
    /// </summary>
    private const string OutcomesOpen = "<!-- the plugin sources that name a reference outcome: one per line, the file first, read by ReferenceCreationTests -->";

    /// <summary>
    /// The comment that closes it.
    /// </summary>
    private const string OutcomesClose = "<!-- end of the sources that name an outcome -->";

    /// <summary>
    /// The type the paragraph is about, spelled as the sources spell it.
    /// </summary>
    private const string Resolver = "ReferenceResolver";

    /// <summary>
    /// The answer type, spelled as the plugin spells it. It is the narrower
    /// name of the two: a source naming <c>ReferenceResolution</c> names this as
    /// well, so the reading catches the answer wherever it is carried rather
    /// than only where the enumeration itself is mentioned.
    /// </summary>
    private const string Outcome = "ReferenceOutcome";

    /// <summary>
    /// The document, copied to the output for the reason every other document
    /// read here is: walking up from the test binary answers a different
    /// question on a machine where the tests run from somewhere else.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "references.md");

    /// <summary>
    /// The plugin's sources, copied beside the test binary by the test project
    /// file.
    /// </summary>
    private static readonly string _sources = Path.Combine(AppContext.BaseDirectory, "plugin-sources");

    /// <summary>
    /// The sources the paragraph names are the sources that name the resolver,
    /// as a set and in both directions. This is the claim that went stale in
    /// its previous spelling, and holding it as a set is what makes the day a
    /// pass carries a resolution to an item the day the paragraph reddens.
    /// </summary>
    [Fact]
    public void TheSourcesTheParagraphNamesAreTheSourcesThatNameTheResolver()
    {
        Assert.Equal(
            Naming().OrderBy(path => path, StringComparer.Ordinal).ToList(),
            Declared().OrderBy(path => path, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The fence is still there and the list inside it is not empty. Without
    /// this leg a renamed comment, or a list somebody emptied, would leave the
    /// comparison above failing for a reason nobody could place.
    /// </summary>
    [Fact]
    public void TheFenceTheComparisonReadsIsStillThere()
    {
        var lines = Lines(_document);

        Assert.Contains(NamesOpen, lines, StringComparer.Ordinal);
        Assert.Contains(NamesClose, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Declared());
    }

    /// <summary>
    /// The comparison reads sources rather than an empty directory. A run that
    /// found no sources at all would make both sides of a negative claim empty
    /// and pass on exactly the arrangement that proves nothing, so what the
    /// scan produced is asserted to be a real reading of the tree.
    /// </summary>
    [Fact]
    public void TheScanReadsThePluginsOwnSources()
    {
        Assert.True(Directory.Exists(_sources));
        Assert.Contains(
            "Jellyfin.Plugin.MetadataSync/References/ReferenceResolver.cs",
            SourceFiles().Select(Relative).ToList(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The sources the paragraph names as naming a reference outcome are the
    /// sources that name one, as a set and in both directions.
    /// </summary>
    /// <remarks>
    /// The paragraph this list sits under said the log an unrecorded outcome
    /// belongs in was not built. #48 built the store and the line it keeps while
    /// that sentence stood, so the page understated the tree in the direction
    /// that costs a second implementation. What is true instead is narrower and
    /// is what this holds: the row that store keeps carries a conflict outcome,
    /// and a reference outcome is named nowhere but where one is produced.
    /// <para>
    /// What it cannot reach. It reads a spelling in a source rather than a
    /// route, so a source that carried an outcome under another name is outside
    /// it, and it says nothing about whether the store's row could hold one. It
    /// is the same bound the list above it states for its own reading.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheSourcesTheParagraphNamesAreTheSourcesThatNameAnOutcome()
    {
        Assert.Equal(
            NamingTheOutcome().OrderBy(path => path, StringComparer.Ordinal).ToList(),
            Declared(OutcomesOpen, OutcomesClose).OrderBy(path => path, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// That fence is still there and the list inside it is not empty, for the
    /// reason the fence above has such a leg: a renamed comment would leave the
    /// comparison failing where nobody could place it, and two empty sets agree.
    /// </summary>
    [Fact]
    public void TheFenceAroundTheOutcomeSourcesIsStillThere()
    {
        var lines = Lines(_document);

        Assert.Contains(OutcomesOpen, lines, StringComparer.Ordinal);
        Assert.Contains(OutcomesClose, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Declared(OutcomesOpen, OutcomesClose));
    }

    /// <summary>
    /// The plugin sources whose text names a reference outcome.
    /// </summary>
    /// <returns>The paths, relative to the repository root.</returns>
    private static List<string> NamingTheOutcome() =>
        SourceFiles()
            .Where(path => File.ReadAllText(path).Contains(Outcome, StringComparison.Ordinal))
            .Select(Relative)
            .ToList();

    /// <summary>
    /// The plugin sources whose text names the resolver.
    /// </summary>
    /// <returns>The paths, relative to the repository root.</returns>
    private static List<string> Naming() =>
        SourceFiles()
            .Where(path => File.ReadAllText(path).Contains(Resolver, StringComparison.Ordinal))
            .Select(Relative)
            .ToList();

    /// <summary>
    /// The entries the fenced list in the document declares.
    /// </summary>
    /// <returns>The entries, as the document quotes them.</returns>
    private static List<string> Declared() => Declared(NamesOpen, NamesClose);

    /// <summary>
    /// The entries a fenced list in the document declares. One reading rather
    /// than one per fence, so a second list cannot drift into being read by a
    /// copy of this that differs from it in a way nobody notices.
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
    /// project file.
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
    /// same on either platform and each line trimmed of trailing space.
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
