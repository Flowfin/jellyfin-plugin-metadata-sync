using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the sentence <c>docs/field-register.md</c> makes about a lock claimed
/// on the peer against the resolver that decides on one.
///
/// The paragraph under <c>What this register does not do</c> said this register
/// does not know the peer's lock state, that nothing here covers that
/// direction, and that a reader should assume it is uncovered rather than
/// covered elsewhere. It was written on 2026-08-08. <c>peer-field-locked</c>
/// was declared the day after and implemented the day after that, the planner
/// has handed the rules a claim the peer reports since the plan landed, and the
/// sweep over the nine lockable names covers that claim. So one document
/// declared a rule on exactly the state the other said nothing covered, and
/// which answer a reader got depended on which file they opened - the
/// arrangement this board measured the cost of when two sets of writable fields
/// disagreed and each was green against the register it was compared to.
///
/// The tables in that document are rendered from <c>field-register.json</c> and
/// compared character for character by <see cref="PublishedRegisterTests"/>.
/// The prose between them is read by nothing, which is the residual #87 records
/// and this narrows by one paragraph rather than closing.
///
/// What it reads, in both directions. The rules the page fences as deciding on
/// a claim the peer reports are compared with the rules the resolver decides
/// from <c>ConflictInputs.FieldLockedOnPeer</c>, derived from the resolver's own
/// source rather than listed here. A second rule reading that claim with no line
/// on the page is red, and a line naming a rule that has stopped reading it is
/// red too, which are two different repairs and the failure says which.
///
/// What it cannot reach, stated rather than left to be assumed. It judges the
/// fenced list and never the prose around it, so the two absences the page
/// states beside it - that no contract fills the input, and that a send is
/// refused nowhere - are unheld here and stay negative on the page. The reading
/// is by the text of the resolver's source, so a rule reaching that claim
/// through a helper the segment does not name is outside it, and it reads the
/// sources this run copied rather than the mainline.
/// </summary>
public class FieldRegisterStatementTests
{
    /// <summary>
    /// The comment that opens the fence around the rules the page says decide
    /// on a lock claimed on the peer.
    /// </summary>
    private const string RulesOpen = "<!-- the conflict rules that decide on a lock claimed on the peer: one per line, the rule first, read by FieldRegisterStatementTests -->";

    /// <summary>
    /// The comment that closes it.
    /// </summary>
    private const string RulesClose = "<!-- end of the rules that decide on a claim the peer reports -->";

    /// <summary>
    /// The marker a fenced line opens with, up to and including the backtick
    /// the rule starts at.
    /// </summary>
    private const string EntryOpen = "- `";

    /// <summary>
    /// The input a rule reads to decide on a claim the peer reports, spelled as
    /// the resolver spells it.
    /// </summary>
    private const string PeerClaim = "FieldLockedOnPeer";

    /// <summary>
    /// The resolver, under the copied sources.
    /// </summary>
    private const string ResolverSource = "Conflicts/ConflictResolver.cs";

    /// <summary>
    /// A rule the resolver declares that reads no claim from the peer, so the
    /// leg proving the reading segments the file at all names one rather than
    /// asserting that something somewhere matched.
    /// </summary>
    private const string RuleReadingNoPeerClaim = "values-agree";

    /// <summary>
    /// The document, copied to the output for the reason every other document
    /// read here is: walking up from the test binary answers a different
    /// question on a machine where the tests run from somewhere else.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "field-register.md");

    /// <summary>
    /// The plugin's sources, copied beside the test binary by the test project
    /// file.
    /// </summary>
    private static readonly string _sources = Path.Combine(AppContext.BaseDirectory, "plugin-sources");

    /// <summary>
    /// The rules the page fences are the rules the resolver decides from a
    /// claim the peer reports, as a set and in both directions. This is the
    /// claim that drifted, and it drifted in the direction where the page
    /// understated what the plugin does.
    /// </summary>
    [Fact]
    public void TheRulesThePageNamesAreTheRulesThatDecideOnAClaimThePeerReports()
    {
        Assert.Equal(
            Deciding().OrderBy(rule => rule, StringComparer.Ordinal).ToList(),
            Declared().OrderBy(rule => rule, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The fence is still there and the list inside it is not empty. Without
    /// this leg a renamed comment, or a list somebody emptied, would leave the
    /// comparison above failing for a reason nobody could place, and two empty
    /// sets would agree.
    /// </summary>
    [Fact]
    public void TheFenceTheComparisonReadsIsStillThere()
    {
        var lines = Lines(_document);

        Assert.Contains(RulesOpen, lines, StringComparer.Ordinal);
        Assert.Contains(RulesClose, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Declared());
    }

    /// <summary>
    /// The reading segments the resolver into its rules rather than answering
    /// out of a file it did not find. A rule that reads no claim from the peer
    /// has to come back from the same reading, so an empty answer on the other
    /// side is the resolver having no such rule rather than the parse having
    /// found no rule at all.
    /// </summary>
    [Fact]
    public void TheReadingSegmentsTheResolverIntoItsRules()
    {
        Assert.True(File.Exists(Path.Combine(_sources, ResolverSource.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Contains(RuleReadingNoPeerClaim, Rules().Select(rule => rule.Id).ToList(), StringComparer.Ordinal);
    }

    /// <summary>
    /// The rules the page fences off, in the order it writes them. A line that
    /// is not an entry, including the wrapped remainder of one, is passed over,
    /// so the reading is of the rule and never of the sentence beside it.
    /// </summary>
    /// <returns>The rule identifiers, as the document writes them.</returns>
    private static List<string> Declared()
    {
        var lines = Lines(_document);
        var opens = lines.IndexOf(RulesOpen);
        var closes = lines.IndexOf(RulesClose);

        if (opens < 0 || closes < opens)
        {
            return new List<string>();
        }

        var rules = new List<string>();

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

            rules.Add(line[EntryOpen.Length..ends]);
        }

        return rules;
    }

    /// <summary>
    /// The rules whose own text reads the claim the peer reports.
    /// </summary>
    /// <returns>The rule identifiers.</returns>
    private static List<string> Deciding() =>
        Rules()
            .Where(rule => rule.Text.Contains(PeerClaim, StringComparison.Ordinal))
            .Select(rule => rule.Id)
            .ToList();

    /// <summary>
    /// The resolver's rules, each with the source text that decides it. A rule
    /// opens at its own key and runs to the next one, so a decision written
    /// over several lines is read whole rather than one line at a time.
    /// </summary>
    /// <returns>The identifier and the text of each rule, in source order.</returns>
    private static List<(string Id, string Text)> Rules()
    {
        var file = Path.Combine(_sources, ResolverSource.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(file))
        {
            return new List<(string, string)>();
        }

        var source = File.ReadAllText(file);
        var keys = new List<(string Id, int At, int Length)>();

        for (var at = source.IndexOf("[\"", StringComparison.Ordinal); at >= 0; at = source.IndexOf("[\"", at + 1, StringComparison.Ordinal))
        {
            var closes = source.IndexOf("\"] =", at, StringComparison.Ordinal);

            if (closes < 0)
            {
                break;
            }

            var id = source[(at + 2)..closes];

            if (id.Contains('"', StringComparison.Ordinal) || id.Contains('\n', StringComparison.Ordinal))
            {
                continue;
            }

            keys.Add((id, at, closes + 4 - at));
        }

        var rules = new List<(string Id, string Text)>();

        for (var i = 0; i < keys.Count; i++)
        {
            var from = keys[i].At + keys[i].Length;
            var to = i + 1 < keys.Count ? keys[i + 1].At : source.Length;

            rules.Add((keys[i].Id, source[from..to]));
        }

        return rules;
    }

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
