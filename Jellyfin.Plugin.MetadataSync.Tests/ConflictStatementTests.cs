using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the absences the closing section of <c>docs/conflicts.md</c> states
/// against the sources they are statements about.
///
/// The two tables in that document are rendered from
/// <c>conflict-rules.json</c> and compared character for character, and the
/// prose around them is read by nothing. That is the residual <c>#87</c>
/// records for the register and <c>#45</c> records for this page, and it has
/// already cost something readable here rather than staying hypothetical: the
/// paragraph this reads said none of the links between the register, the rules
/// and the writer were chained while three of them were, and it was repaired by
/// somebody working on a neighbouring change rather than by anybody reading the
/// file. The drift then was the page being behind the tree. What this holds is
/// the same paragraph drifting the other way.
///
/// What it reads. Every spelling the page says appears nowhere in this plugin's
/// own sources, as an assertion that none of them does. The day a pass
/// constructs the observation a plan is made from, the page reds rather than
/// going on saying nothing does, and the repair is made by whoever lands the
/// thing rather than by whoever next reads the page.
///
/// The list is one line, and the entry that is not on it is the reason to read
/// this paragraph. That nothing schedules or starts a pass is the same claim,
/// and it is already refused by
/// <see cref="SecurityPolicyTests.TheAbsencesTheSectionRestsOnAreStillAbsences"/>
/// against the absences <c>SECURITY.md</c> rests on. It was written into the
/// fence here first and taken out when the breakage that was meant to prove
/// this leg bites reddened that one too, which is how a second declaration of
/// one fact is found before it lands rather than after the two disagree.
///
/// That is a negative disclosure and it stays one. What is asserted is the
/// absence the page states and never that the absence is harmless, so a
/// spelling that has arrived is a failure naming it rather than a line quietly
/// deleted from the list.
///
/// The leg an absence guard fails silently on is the one where it read nothing
/// at all. An empty answer from a scan over an empty directory reads exactly
/// like an empty answer from a scan over the whole plugin, so the same reading
/// is run against a spelling the sources do carry and is required to find it.
///
/// What it cannot reach, stated rather than left to be assumed. It judges the
/// fenced list and never the prose around it, so the claims in that paragraph
/// which are not spellings - that nothing reads the peer, and that a decision is
/// recorded nowhere - are unread here, and the page says so under the fence.
/// The reading is by the text of the source, the same bound
/// <see cref="ReconciliationStatementTests"/> states for its own, so a
/// construction reached through an alias, a factory or reflection is outside
/// it. And it reads the sources this run copied rather than the mainline.
/// </summary>
public class ConflictStatementTests
{
    /// <summary>
    /// The comment that opens the fence around the spellings the page says are
    /// nowhere in the plugin.
    /// </summary>
    private const string AbsentOpen = "<!-- the spellings this page says appear nowhere in the plugin's sources: one per line, the spelling first, read by ConflictStatementTests -->";

    /// <summary>
    /// The comment that closes it.
    /// </summary>
    private const string AbsentClose = "<!-- end of the spellings that appear nowhere -->";

    /// <summary>
    /// The marker a fenced line opens with, up to and including the backtick
    /// the spelling starts at.
    /// </summary>
    private const string EntryOpen = "- `";

    /// <summary>
    /// A spelling the plugin's sources do carry, read by the same function the
    /// absences are read by. It is the fenced entry minus the one word that
    /// makes it a construction, so the control differs from the claim in the
    /// characters that decide it rather than being an unrelated name.
    /// </summary>
    private const string Control = "ItemObservation";

    /// <summary>
    /// A source the control is expected in, so the leg names a file rather than
    /// asserting that something somewhere matched.
    /// </summary>
    private const string ControlIsIn = "Jellyfin.Plugin.MetadataSync/Reconciliation/Planner.cs";

    /// <summary>
    /// The prefix every scanned source is written under.
    /// </summary>
    private const string ProjectPrefix = "Jellyfin.Plugin.MetadataSync/";

    /// <summary>
    /// The document, copied to the output for the reason every other document
    /// read here is: walking up from the test binary answers a different
    /// question on a machine where the tests run from somewhere else.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "conflicts.md");

    /// <summary>
    /// The plugin's sources, copied beside the test binary by the test project
    /// file.
    /// </summary>
    private static readonly string _sources = Path.Combine(AppContext.BaseDirectory, "plugin-sources");

    /// <summary>
    /// Every spelling the page says appears nowhere in the plugin appears
    /// nowhere in the plugin. The failure names the spellings that arrived and
    /// the files they arrived in, because the repair is a rewrite of the
    /// paragraph rather than a deletion of the line.
    /// </summary>
    [Fact]
    public void NoSpellingThisPageSaysIsAbsentIsInThePluginsSources()
    {
        var arrived = Declared()
            .Select(spelling => new { Spelling = spelling, In = Naming(spelling) })
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
    public void TheFenceTheComparisonReadsIsStillThere()
    {
        var lines = Lines(_document);

        Assert.Contains(AbsentOpen, lines, StringComparer.Ordinal);
        Assert.Contains(AbsentClose, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Declared());
    }

    /// <summary>
    /// The reading that answers with nothing would answer with something if
    /// there were something. This is what separates an absence in the plugin
    /// from a scan that reached no source, which is the way an absence guard
    /// passes for the wrong reason and the one failure the assertion above
    /// cannot tell apart on its own.
    /// </summary>
    [Fact]
    public void TheReadingFindsASpellingThatIsThere()
    {
        Assert.True(Directory.Exists(_sources));
        Assert.Contains(ControlIsIn, Naming(Control), StringComparer.Ordinal);
    }

    /// <summary>
    /// The spellings the page fences off, in the order it writes them. A line
    /// that is not an entry, including the wrapped remainder of one, is passed
    /// over, so the reading is of the spelling and never of the sentence beside
    /// it.
    /// </summary>
    /// <returns>The spellings, as the document writes them.</returns>
    private static List<string> Declared()
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
    private static List<string> Naming(string spelling) =>
        SourceFiles()
            .Where(path => File.ReadAllText(path).Contains(spelling, StringComparison.Ordinal))
            .Select(Relative)
            .ToList();

    /// <summary>
    /// A copied source read back the way the paths in a failure are written,
    /// with one separator whichever platform it ran on.
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
