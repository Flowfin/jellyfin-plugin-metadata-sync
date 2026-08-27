using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the paragraph of <c>docs/matching.md</c> that says what the lint's
/// wider reach is for against the walk that reach is measured against.
///
/// That paragraph gave its reason as there being no resolution path in this
/// tree yet. There was one: <see cref="ResolutionPathTests"/> starts at the
/// types this plugin declares under two namespaces and follows what a call from
/// them arrives at. So the document handed a reader the opposite of the tree
/// beside it, in the file that is the first thing somebody deciding where an
/// identity may come from opens. Nothing reported it, because nothing read the
/// paragraph.
///
/// What it reads, in both directions. The namespaces the walk is seeded with
/// are compared with the list the paragraph carries, so a third namespace added
/// to the seed is red and a line naming one the seed has dropped is red too.
/// The seed is read from the walk rather than restated here, because two
/// declarations of which namespaces resolve is the arrangement where a reader's
/// answer depends on which file they opened.
///
/// What it cannot reach, stated rather than left to be assumed. It judges the
/// list and never the prose around it, so a sentence with no list under it is
/// unread, which is the same bound <see cref="ReferenceCreationTests"/> states
/// for its own fence. It says nothing about whether the namespaces the seed
/// names are the ones that ought to resolve: a resolver written into a third
/// namespace and left out of the seed is invisible to both sides of this
/// comparison, and what stands against that is the lint, which reads every
/// plugin source rather than these two.
/// </summary>
public class MatchingStatementTests
{
    /// <summary>
    /// The comment that opens the fence around the namespaces the walk starts
    /// from.
    /// </summary>
    private const string NamespacesOpen = "<!-- the namespaces the resolution walk starts from: one per line, read by MatchingStatementTests -->";

    /// <summary>
    /// The comment that closes it.
    /// </summary>
    private const string NamespacesClose = "<!-- end of the namespaces the resolution walk starts from -->";

    /// <summary>
    /// The field on the walk that holds the seed, named as that file names it.
    /// A rename is a red leg below rather than two empty sets agreeing.
    /// </summary>
    private const string SeedField = "ResolutionPaths";

    /// <summary>
    /// The document, copied to the output for the reason every other document
    /// read here is: walking up from the test binary answers a different
    /// question on a machine where the tests run from somewhere else.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "matching.md");

    /// <summary>
    /// The namespaces the paragraph names are the namespaces the walk is seeded
    /// with, as a set and in both directions. This is the claim that went stale
    /// in its previous spelling, and holding it as a set is what makes the day
    /// a third resolver namespace arrives the day the paragraph reddens.
    /// </summary>
    [Fact]
    public void TheNamespacesTheParagraphNamesAreTheOnesTheWalkStartsFrom()
    {
        Assert.Equal(
            Seeded().OrderBy(name => name, StringComparer.Ordinal).ToList(),
            Declared().OrderBy(name => name, StringComparer.Ordinal).ToList());
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

        Assert.Contains(NamespacesOpen, lines, StringComparer.Ordinal);
        Assert.Contains(NamespacesClose, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Declared());
    }

    /// <summary>
    /// The seed is read rather than assumed. A field that was renamed or
    /// emptied would make one side of the comparison above empty, which reads
    /// as a document naming namespaces nobody walks rather than as a reading
    /// that failed.
    /// </summary>
    [Fact]
    public void TheSeedTheComparisonReadsIsStillOnTheWalk()
    {
        Assert.NotEmpty(Seeded());
    }

    /// <summary>
    /// Every namespace the paragraph names is one this plugin declares a type
    /// in. A prefix that matches nothing walks nothing, and the rule above
    /// would go on passing with the document and the seed agreeing about a
    /// namespace that had moved.
    /// </summary>
    [Fact]
    public void EveryNamespaceTheParagraphNamesIsOneThePluginDeclaresTypesIn()
    {
        var declared = AssemblyMetadata.TypeNames(typeof(Plugin).Assembly);

        var empty = Declared()
            .Where(prefix => !declared.Any(name => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(empty);
    }

    /// <summary>
    /// The namespaces the walk is seeded with, read off the walk itself.
    /// </summary>
    /// <returns>The namespace prefixes, as that file spells them.</returns>
    private static List<string> Seeded()
    {
        var field = typeof(ResolutionPathTests).GetField(SeedField, BindingFlags.NonPublic | BindingFlags.Static);

        return field?.GetValue(null) is string[] seeded ? seeded.ToList() : new List<string>();
    }

    /// <summary>
    /// The entries the fenced list in the document declares.
    /// </summary>
    /// <returns>The entries, as the document quotes them.</returns>
    private static List<string> Declared()
    {
        var lines = Lines(_document);
        var opens = lines.IndexOf(NamespacesOpen);
        var closes = lines.IndexOf(NamespacesClose);

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
