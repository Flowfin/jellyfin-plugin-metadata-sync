using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the changelog's classes together across the three files that carry
/// them: the document a person reads, the drafter configuration a machine reads,
/// and the label file that is the authority for which labels exist at all.
/// </summary>
/// <remarks>
/// A class declared in one of the three and missing from another is the failure
/// this is for. It is silent in both directions: a class argued in the document
/// with no category sorts nothing, and a category with no row sorts entries
/// under a heading nothing explains. Neither shows up until somebody reads a
/// release note and finds an entry in the wrong place or nowhere at all.
/// <para>
/// The bound, stated rather than left to be assumed. These are line scans and
/// not YAML parses, so they answer what the files say and never what the drafter
/// does with them. A category written in a shape this scan does not read is
/// invisible to it, which is why the legs that count are here as well as the
/// legs that match. Nothing here judges whether an entry was put in the right
/// class or whether the sentence in one is true.
/// </para>
/// </remarks>
public class ChangelogTests
{
    private const string ClassHeader = "| Class | Label | What it is for |";

    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "changelog.md");

    private static readonly string _drafter = Path.Combine(AppContext.BaseDirectory, "release-drafter.yml");

    private static readonly string _labels = Path.Combine(AppContext.BaseDirectory, "labels.yaml");

    /// <summary>
    /// Every class the document argues for has a category the drafter reads. A
    /// class argued and not applied sorts nothing, and the release note reads as
    /// though the distinction was never made.
    /// </summary>
    [Fact]
    public void EveryClassInTheDocumentHasACategoryInTheDrafter()
    {
        var categories = Categories();

        foreach (var row in ClassRows())
        {
            Assert.True(categories.ContainsKey(row.Class), "No drafter category is titled: " + row.Class);
            Assert.Contains(row.Label, categories[row.Class], StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The other direction. A category with no row sorts entries under a heading
    /// nothing on this board explains, and the first person to ask what it means
    /// has nowhere to look.
    /// </summary>
    [Fact]
    public void EveryCategoryInTheDrafterHasARowInTheDocument()
    {
        var declared = ClassRows().ToDictionary(r => r.Class, r => r.Label, StringComparer.Ordinal);

        foreach (var (title, labels) in Categories())
        {
            Assert.True(declared.ContainsKey(title), "The document has no row for the category: " + title);
            Assert.Contains(declared[title], labels, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Every label either side names is declared in the label file, which is the
    /// authority for which labels exist on this board and which deletes any that
    /// are not in it. A category keyed on a label nobody can apply routes
    /// nothing, and it looks exactly like one that is simply never used.
    /// </summary>
    [Fact]
    public void EveryLabelNamedIsDeclaredInTheLabelFile()
    {
        var declared = DeclaredLabels();

        Assert.NotEmpty(declared);

        foreach (var row in ClassRows())
        {
            Assert.Contains(row.Label, declared, StringComparer.Ordinal);
        }

        foreach (var label in Categories().Values.SelectMany(l => l))
        {
            Assert.Contains(label, declared, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Every class has an example. A class with a sentence saying what it is for
    /// and nothing showing what one looks like is where two contributors write
    /// two different shapes and both believe they followed the document.
    /// </summary>
    [Fact]
    public void EveryClassHasAnExampleInTheDocument()
    {
        var text = File.ReadAllText(_document);

        foreach (var row in ClassRows())
        {
            Assert.Contains("**" + row.Class + "**", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The three classes this repository has its own configuration for at all.
    /// A drafter's defaults sort a change by whether it is a feature or a fix,
    /// and each of these three can arrive spelled as a fix while being the thing
    /// an operator has to read before upgrading.
    /// </summary>
    [Fact]
    public void TheThreeClassesThatMarkAnUpgradeDecisionArePresent()
    {
        var classes = ClassRows().Select(r => r.Class).ToList();

        Assert.Contains("Contract change", classes, StringComparer.Ordinal);
        Assert.Contains("Field register change", classes, StringComparer.Ordinal);
        Assert.Contains("Conflict rule change", classes, StringComparer.Ordinal);
    }

    /// <summary>
    /// Both scans read something. Every leg above is satisfied by a read that
    /// returned nothing, and a matching test over two empty sets is the shape
    /// that passes for the wrong reason forever.
    /// </summary>
    [Fact]
    public void BothScansReadSomething()
    {
        Assert.NotEmpty(ClassRows());
        Assert.NotEmpty(Categories());
    }

    /// <summary>
    /// The drafter writes the tag shape this repository's release route refuses
    /// anything else in. A drafter naming a release one way beside a publish
    /// workflow that requires another produces two spellings of one release.
    /// </summary>
    [Fact]
    public void TheDrafterWritesTheTagShapeTheReleaseRouteRequires()
    {
        var lines = File.ReadAllLines(_drafter);

        Assert.Contains(lines, l => l.Trim().Equals("tag-template: '$RESOLVED_VERSION-stable'", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Trim().Equals("name-template: '$RESOLVED_VERSION-stable'", StringComparison.Ordinal));
    }

    /// <summary>
    /// An entry nobody classified is still an entry. A drafter configured to
    /// drop one produces a release note that reads as complete and is not.
    /// </summary>
    [Fact]
    public void NoLabelExcludesAnEntryFromTheReleaseNote()
    {
        var lines = File.ReadAllLines(_drafter);

        Assert.Contains(lines, l => l.Trim().Equals("exclude-labels: []", StringComparison.Ordinal));
    }

    private static IReadOnlyList<(string Class, string Label)> ClassRows()
    {
        var lines = File.ReadAllLines(_document);
        var start = Array.FindIndex(lines, l => string.Equals(l.Trim(), ClassHeader, StringComparison.Ordinal));
        Assert.True(start >= 0, "The document has no table headed: " + ClassHeader);

        var rows = new List<(string, string)>();
        for (var i = start + 2; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('|'))
            {
                break;
            }

            var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            Assert.True(cells.Length == 3, "A class row does not carry three cells: " + line);
            rows.Add((cells[0], cells[1].Trim('`')));
        }

        return rows;
    }

    // One entry per category title, holding the labels written under it. The
    // scan follows the file's own indentation: a title opens a category and the
    // list items after it belong to that title until the next one opens.
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Categories()
    {
        var title = new Regex(@"^\s*-\s+title:\s+'([^']*)'\s*$");
        var label = new Regex(@"^\s+-\s+'([^']*)'\s*$");

        var categories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var current = string.Empty;

        foreach (var line in File.ReadAllLines(_drafter))
        {
            var opened = title.Match(line);
            if (opened.Success)
            {
                current = opened.Groups[1].Value;
                Assert.False(categories.ContainsKey(current), "Two categories carry the title: " + current);
                categories[current] = new List<string>();
                continue;
            }

            var named = label.Match(line);
            if (named.Success && current.Length > 0)
            {
                ((List<string>)categories[current]).Add(named.Groups[1].Value);
            }
        }

        return categories;
    }

    private static IReadOnlyCollection<string> DeclaredLabels()
    {
        var name = new Regex(@"^-\s+name:\s+""([^""]*)""\s*$");

        return File.ReadAllLines(_labels)
            .Select(line => name.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .ToList();
    }
}
