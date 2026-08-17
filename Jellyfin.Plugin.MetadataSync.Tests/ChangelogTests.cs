using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the changelog's classes together across the four files that carry
/// them: the document a person reads, the drafter configuration a machine reads,
/// the label file that is the authority for which labels exist at all, and the
/// hygiene gate that refuses a pull request carrying no class.
/// </summary>
/// <remarks>
/// A class declared in one of the four and missing from another is the failure
/// this is for. It is silent in both directions: a class argued in the document
/// with no category sorts nothing, and a category with no row sorts entries
/// under a heading nothing explains. Neither shows up until somebody reads a
/// release note and finds an entry in the wrong place or nowhere at all.
/// <para>
/// The gate is the fourth copy and the one furthest from the document. It runs
/// with nothing checked out, so it carries the list rather than reading it, and
/// a class added to the document alone would be a class the gate lets a pull
/// request omit.
/// </para>
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

    private static readonly string _gate = Path.Combine(AppContext.BaseDirectory, "pr-hygiene.yml");

    // Where a file the gate keys a class to is copied to. The copy is declared
    // in this project by the same path the gate names it by, so the leg below
    // is answered by the file rather than by a second list beside it. What the
    // leg catches is a path added to the gate and copied nowhere. A file that
    // moved without the project moving never reaches it: the copy refuses
    // first, as MSB3030, and the build stops there.
    private static readonly string _subjects = Path.Combine(AppContext.BaseDirectory, "subjects");

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
    /// Every class the document argues for is one the hygiene gate will accept
    /// on a pull request. A class added to the document alone is one the gate
    /// refuses every change that carries it, which reads as the label being
    /// wrong rather than as the gate being behind.
    /// </summary>
    [Fact]
    public void EveryClassInTheDocumentIsAcceptedByTheGate()
    {
        var accepted = GateClassLabels();

        foreach (var row in ClassRows())
        {
            Assert.Contains(row.Label, accepted, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The other direction. A label the gate accepts and the document does not
    /// argue for lets a change through in a class nothing explains, and the
    /// drafter has no category to sort it under either.
    /// </summary>
    [Fact]
    public void EveryClassTheGateAcceptsHasARowInTheDocument()
    {
        var declared = ClassRows().Select(r => r.Label).ToList();

        foreach (var label in GateClassLabels())
        {
            Assert.Contains(label, declared, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Every file the gate keys a class to is a file in this tree, and the class
    /// it keys to it is one the document argues for. A rule pointed at a path
    /// nothing writes to never fires, and it reads exactly like a rule that is
    /// simply never tripped.
    /// </summary>
    [Fact]
    public void EverySubjectTheGateKeysAClassToIsAPathInThisTree()
    {
        var declared = ClassRows().Select(r => r.Label).ToList();
        var keyed = GateSubjects();

        Assert.NotEmpty(keyed);

        foreach (var (label, paths) in keyed)
        {
            Assert.Contains(label, declared, StringComparer.Ordinal);
            Assert.NotEmpty(paths);

            foreach (var path in paths)
            {
                Assert.True(
                    File.Exists(Path.Combine(_subjects, path.Replace('/', Path.DirectorySeparatorChar))),
                    "The gate keys a class to a path this tree does not carry: " + path);
            }
        }
    }

    /// <summary>
    /// Every scan reads something. Every leg above is satisfied by a read that
    /// returned nothing, and a matching test over two empty sets is the shape
    /// that passes for the wrong reason forever.
    /// </summary>
    [Fact]
    public void EveryScanReadsSomething()
    {
        Assert.NotEmpty(ClassRows());
        Assert.NotEmpty(Categories());
        Assert.NotEmpty(GateClassLabels());
        Assert.NotEmpty(GateSubjects());
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

        // Built on the concrete list type and widened once on the way out. The
        // scan appends to a category after the line that opened it, and a
        // dictionary typed to the read-only interface can only offer that by
        // casting its own values back to something it does not promise.
        var categories = new Dictionary<string, List<string>>(StringComparer.Ordinal);
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
                categories[current].Add(named.Groups[1].Value);
            }
        }

        return categories.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)entry.Value,
            StringComparer.Ordinal);
    }

    // The classes the gate accepts on a pull request, read out of the list it
    // carries. The scan follows the list's own punctuation and stops at its
    // close, so a label written anywhere else in the file is not read as one.
    private static IReadOnlyList<string> GateClassLabels()
    {
        return Block("const CLASS_LABELS = [")
            .Select(line => new Regex(@"^""(.+)"",$").Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .ToList();
    }

    // One entry per class the gate keys to a file, holding the paths written
    // under it. A label opens an entry and the quoted lines after it are its
    // paths until the next label opens.
    private static IReadOnlyList<(string Label, IReadOnlyList<string> Paths)> GateSubjects()
    {
        var label = new Regex(@"^label: ""(.+)"",$");
        var path = new Regex(@"^""(.+)"",$");

        // Same reason as in Categories: the paths of an entry are appended to
        // after the line that opened it, so the list is held as a list until
        // the return widens it.
        var keyed = new List<(string Label, List<string> Paths)>();
        foreach (var line in Block("const CLASS_BY_SUBJECT = ["))
        {
            var opened = label.Match(line);
            if (opened.Success)
            {
                keyed.Add((opened.Groups[1].Value, new List<string>()));
                continue;
            }

            var named = path.Match(line);
            if (named.Success && keyed.Count > 0)
            {
                keyed[^1].Paths.Add(named.Groups[1].Value);
            }
        }

        return keyed.Select(entry => (entry.Label, (IReadOnlyList<string>)entry.Paths)).ToList();
    }

    // The lines of one declaration in the gate, trimmed, from the line that
    // opens it to the line that closes it. The close is the one punctuation the
    // nested lists inside a declaration do not use.
    private static IReadOnlyList<string> Block(string opener)
    {
        var lines = File.ReadAllLines(_gate).Select(l => l.Trim()).ToList();
        var start = lines.FindIndex(l => string.Equals(l, opener, StringComparison.Ordinal));
        Assert.True(start >= 0, "The gate declares nothing opened by: " + opener);

        var end = lines.FindIndex(start, l => string.Equals(l, "];", StringComparison.Ordinal));
        Assert.True(end > start, "The declaration opened by " + opener + " is never closed.");

        return lines.GetRange(start + 1, end - start - 1);
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
