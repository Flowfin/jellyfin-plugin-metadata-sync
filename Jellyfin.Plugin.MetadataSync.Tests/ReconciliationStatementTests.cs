using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Re-runs the reading <c>docs/reconciliation.md</c> pastes under its opening
/// section against the sources it is a reading of.
///
/// That paste is the one on this board with a measured drift already behind it.
/// It named one file while the command returned three, and the paragraph under
/// it says so: it was found by somebody running the line while adding a fourth,
/// not by anybody reading the page. Nothing read the file then and nothing read
/// it after the repair, so the same paste could go stale again the next time a
/// file asks the server's library for something.
///
/// What it reads, in both directions. The paths the paste lists are compared
/// with the plugin sources that name the library, so a fifth file naming it
/// with no line in the paste is red and a line naming a file that has stopped
/// naming it is red too. Which of the two failed is the difference between a
/// page that is behind the tree and a page that is ahead of it, and both are
/// worth a different repair.
///
/// The sentence above the paste carried a count of the files, and the count is
/// gone rather than checked. A numeral beside a list is a second answer to the
/// question the list answers, it drifts on the change the list survives, and
/// deleting it costs a reader nothing that the list does not already give them.
///
/// It also holds the absences the paragraph under that paste states. Those are
/// spellings rather than judgements: a construction of the read or of the write
/// path is a line somebody writes, so the day a pass builds either, the
/// paragraph reds instead of going on saying nothing does. That is a negative
/// disclosure and it stays one - what is asserted is the absence the page states
/// and never that the absence is harmless.
///
/// The third claim that stood in that sentence is deliberately not read here.
/// That nothing turns items into the observations a plan is made from is the
/// same claim <c>docs/conflicts.md</c> fences and
/// <see cref="ConflictStatementTests"/> holds, and a second copy is the
/// arrangement where the unheld one goes stale in silence while the other reds.
/// The page points there instead.
///
/// The absence list carries one bound that the paste beside it does not, and it
/// is stated on the page as well. A comparison of two sets reds in both
/// directions; a list of absences reds in one. Every spelling written down is
/// refused, and a line taken out stops being checked, so narrowing the list is a
/// change to what the page claims rather than a failure - the same bound the
/// rule table in <c>docs/conflicts.md</c> states about a rule that stops being
/// declared, and the same one <see cref="ConflictStatementTests"/> carries.
///
/// What it cannot reach, stated rather than left to be assumed. The reading is
/// by the spelling of the interface's name in the source text, which is what
/// the pasted command does too, so a library reached through an alias or handed
/// in as something else is outside both. It judges this one paste and one fenced
/// list, and never the prose around them, so every other sentence in that
/// document is still read by nothing, which is the residual <c>#87</c> records
/// and this does not narrow. And it compares against the sources this run copied
/// rather than against the mainline.
/// </summary>
public class ReconciliationStatementTests
{
    /// <summary>
    /// The pasted command, spelled as the document spells it, indent included.
    /// The line is the anchor for the reading below, so a paste moved or
    /// reworded is a failure rather than a silent skip.
    /// </summary>
    private const string PastedCommand = "    git grep -Iln \"ILibraryManager\" -- 'Jellyfin.Plugin.MetadataSync/*.cs'";

    /// <summary>
    /// The type the paste is a reading of, spelled as the sources spell it.
    /// </summary>
    private const string Library = "ILibraryManager";

    /// <summary>
    /// The prefix every path in the paste and every scanned source is written
    /// under.
    /// </summary>
    private const string ProjectPrefix = "Jellyfin.Plugin.MetadataSync/";

    /// <summary>
    /// The comment that opens the fence around the spellings the paragraph says
    /// are nowhere in the plugin.
    /// </summary>
    private const string AbsentOpen = "<!-- the spellings this page says appear nowhere in the plugin's sources: one per line, the spelling first, read by ReconciliationStatementTests -->";

    /// <summary>
    /// The comment that closes it.
    /// </summary>
    private const string AbsentClose = "<!-- end of the spellings that appear nowhere -->";

    /// <summary>
    /// The marker a fenced line opens with, up to and including the backtick the
    /// spelling starts at.
    /// </summary>
    private const string EntryOpen = "- `";

    /// <summary>
    /// A spelling the plugin's sources do carry, read by the same function the
    /// absences are read by. It is a fenced entry minus the word that makes it a
    /// construction, so the control differs from the claim in the characters that
    /// decide it rather than being an unrelated name.
    /// </summary>
    private const string Control = "LibraryPlanTarget";

    /// <summary>
    /// The document, copied to the output for the reason every other document
    /// read here is: walking up from the test binary answers a different
    /// question on a machine where the tests run from somewhere else.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "reconciliation.md");

    /// <summary>
    /// The plugin's sources, copied beside the test binary by the test project
    /// file.
    /// </summary>
    private static readonly string _sources = Path.Combine(AppContext.BaseDirectory, "plugin-sources");

    /// <summary>
    /// The paths the paste lists are the plugin sources that name the library,
    /// as a set and in both directions. This is the claim that drifted once
    /// already, and it drifted in the direction where the page was behind the
    /// tree.
    /// </summary>
    [Fact]
    public void ThePathsThePasteListsAreTheSourcesThatNameTheLibrary()
    {
        Assert.Equal(
            Naming().OrderBy(path => path, StringComparer.Ordinal).ToList(),
            Pasted().OrderBy(path => path, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The pasted command is still in the document and still has a result under
    /// it. Without this leg a paste somebody moved, reworded or emptied would
    /// leave the comparison above failing for a reason nobody could place, and
    /// an empty read would compare two empty sets and assert nothing.
    /// </summary>
    [Fact]
    public void ThePasteTheComparisonReadsIsStillThere()
    {
        Assert.Contains(PastedCommand, Lines(_document), StringComparer.Ordinal);
        Assert.NotEmpty(Pasted());
    }

    /// <summary>
    /// The comparison reads sources rather than an empty directory. A run that
    /// found none would leave one side of the comparison empty for a reason
    /// that has nothing to do with the document, so what the scan produced is
    /// asserted to be a real reading of the tree.
    /// </summary>
    [Fact]
    public void TheScanReadsThePluginsOwnSources()
    {
        Assert.True(Directory.Exists(_sources));
        Assert.Contains(
            ProjectPrefix + "Reconciliation/LibraryPlanTarget.cs",
            SourceFiles().Select(Relative).ToList(),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Every spelling the paragraph says appears nowhere in the plugin appears
    /// nowhere in the plugin. The failure names the spellings that arrived and
    /// the files they arrived in, because the repair is a rewrite of the
    /// paragraph rather than a deletion of the line.
    /// </summary>
    [Fact]
    public void NoSpellingThisPageSaysIsAbsentIsInThePluginsSources()
    {
        var arrived = Fenced()
            .Select(spelling => new { Spelling = spelling, In = Carrying(spelling) })
            .Where(found => found.In.Count > 0)
            .Select(found => found.Spelling + " in " + string.Join(", ", found.In))
            .ToList();

        Assert.Empty(arrived);
    }

    /// <summary>
    /// The fence is still there and the list inside it is not empty. Without
    /// this leg a renamed comment, or a list somebody emptied, would leave the
    /// assertion above passing over a read of nothing rather than asserting
    /// anything at all.
    /// </summary>
    [Fact]
    public void TheFenceTheAbsenceComparisonReadsIsStillThere()
    {
        var lines = Lines(_document);

        Assert.Contains(AbsentOpen, lines, StringComparer.Ordinal);
        Assert.Contains(AbsentClose, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Fenced());
    }

    /// <summary>
    /// The reading that answers with nothing would answer with something if
    /// there were something. An empty answer from a scan over an empty directory
    /// reads exactly like an empty answer from a scan over the whole plugin, and
    /// that is the way an absence guard passes for the wrong reason.
    /// </summary>
    [Fact]
    public void TheAbsenceReadingFindsASpellingThatIsThere()
    {
        Assert.Contains(
            ProjectPrefix + "Reconciliation/LibraryPlanTarget.cs",
            Carrying(Control),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The spellings the paragraph fences off, in the order it writes them. A
    /// line that is not an entry, including the wrapped remainder of one, is
    /// passed over, so the reading is of the spelling and never of the sentence
    /// beside it.
    /// </summary>
    /// <returns>The spellings, as the document writes them.</returns>
    private static List<string> Fenced()
    {
        var lines = Lines(_document);
        var opens = lines.IndexOf(AbsentOpen);
        var closes = lines.IndexOf(AbsentClose);

        if (opens < 0 || closes < opens)
        {
            return new List<string>();
        }

        var spellings = new List<string>();

        foreach (var line in lines.Skip(opens + 1).Take(closes - opens - 1))
        {
            if (!line.StartsWith(EntryOpen, StringComparison.Ordinal))
            {
                continue;
            }

            var ends = line.IndexOf('`', EntryOpen.Length);

            if (ends < 0)
            {
                continue;
            }

            spellings.Add(line[EntryOpen.Length..ends]);
        }

        return spellings;
    }

    /// <summary>
    /// The plugin sources whose text carries a spelling.
    /// </summary>
    /// <param name="spelling">The text to look for.</param>
    /// <returns>The paths, relative to the repository root.</returns>
    private static List<string> Carrying(string spelling) =>
        SourceFiles()
            .Where(path => File.ReadAllText(path).Contains(spelling, StringComparison.Ordinal))
            .Select(Relative)
            .ToList();

    /// <summary>
    /// The plugin sources whose text names the library.
    /// </summary>
    /// <returns>The paths, relative to the repository root.</returns>
    private static List<string> Naming() =>
        SourceFiles()
            .Where(path => File.ReadAllText(path).Contains(Library, StringComparison.Ordinal))
            .Select(Relative)
            .ToList();

    /// <summary>
    /// The paths pasted under the command. The block ends at the first line
    /// that is not an indented path under the project, so a blank line or the
    /// prose after it closes the reading without a marker of its own.
    /// </summary>
    /// <returns>The paths, as the document writes them.</returns>
    private static List<string> Pasted()
    {
        var lines = Lines(_document);
        var at = lines.IndexOf(PastedCommand);

        if (at < 0)
        {
            return new List<string>();
        }

        var head = "    " + ProjectPrefix;
        var paths = new List<string>();

        foreach (var line in lines.Skip(at + 1))
        {
            if (!line.StartsWith(head, StringComparison.Ordinal))
            {
                break;
            }

            paths.Add(line.Trim());
        }

        return paths;
    }

    /// <summary>
    /// A copied source read back as the paste writes it, with one separator
    /// whichever platform it ran on.
    /// </summary>
    /// <param name="path">The copied file.</param>
    /// <returns>The path relative to the repository root.</returns>
    private static string Relative(string path) =>
        ProjectPrefix + Path.GetRelativePath(_sources, path).Replace('\\', '/');

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
