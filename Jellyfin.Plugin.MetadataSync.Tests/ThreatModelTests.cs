using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the defence entries of <c>docs/threat-model.md</c> against the tree
/// they describe.
///
/// That document exists so a reader can tell a control from a plan, and it had
/// stopped being able to do that. Its first adversary entry named four defences
/// and said none of them existed, while three were in the tree: the field
/// register, the refusal for a work more than one local item answers to, and
/// the refusal of a field the operator locked. The same entry described the
/// conflict floor as one of three answers under consideration, after that
/// answer had been taken and written into the resolver.
///
/// It was the fifth document in this repository found describing a tree that
/// had moved under it, after the field register twice, the reconciliation
/// document, the readme and the security policy. Every one was found by
/// somebody reading it while working on something else, so the interval between
/// a paragraph going stale and somebody meeting it was bounded by nothing. This
/// is that bound for the defence entries of one file, and it is the second
/// guard of the shape <see cref="SecurityPolicyTests"/> built.
///
/// What it reads, in the directions that fail differently. A defence the
/// document declares as being in the tree has to be a file in the tree, so one
/// deleted or renamed reddens here rather than leaving the document claiming
/// it. A defence declared as owed has to name an issue and no file, so
/// converting an entry is a change to the entry rather than a path quietly
/// appearing beside the word owed. The absence the entry rests on is
/// re-derived rather than read, so a payload type arriving without the
/// paragraph being rewritten reddens. And the conflict floor paragraph names
/// the site the answer is written at and the guard that holds it up, and both
/// have to still be there.
///
/// What it cannot reach, stated rather than left to be assumed. It judges the
/// path on each entry and never the sentence beside it, so a defence described
/// wrongly passes. It cannot tell that the list is complete: a defence this
/// adversary meets that nobody wrote an entry for is invisible here, and no
/// reading of this tree produces the set of defences a threat model owes. The
/// other two adversaries carry no fenced list, because what stops them is
/// nothing and an empty list is not a subject. It says nothing about whether
/// the four in the tree are reached by anything, which is what the paragraph
/// under the list is for and is why that paragraph is negative. And the
/// absence leg refuses one spelling: a payload validation written without the
/// word passes it, the same way the transport walk refuses a type and not an
/// address written as a bare string.
/// </summary>
public class ThreatModelTests
{
    /// <summary>
    /// The prefix a path on an entry carries, which is the project directory.
    /// It is written out rather than trimmed to the last two segments, so a
    /// path naming some other project is refused rather than silently read as
    /// one of this plugin's.
    /// </summary>
    private const string ProjectPrefix = "Jellyfin.Plugin.MetadataSync/";

    /// <summary>
    /// The comment that opens the fence around the defence entries, so a
    /// reading of it is a reading of what the entry declares rather than of
    /// every bullet in the file.
    /// </summary>
    private const string ListOpens = "<!-- the defences of this adversary: one per line, the state first, then the file or the issue, read by ThreatModelTests -->";

    /// <summary>
    /// The comment that closes the fence.
    /// </summary>
    private const string ListCloses = "<!-- end of the defences -->";

    /// <summary>
    /// How an entry declaring a defence that is in the tree opens.
    /// </summary>
    private const string InTheTree = "- in the tree, ";

    /// <summary>
    /// How an entry declaring a defence that is owed opens.
    /// </summary>
    private const string Owed = "- owed, ";

    /// <summary>
    /// The character a path and a guard are quoted with in the document.
    /// </summary>
    private const string Quote = "`";

    /// <summary>
    /// What the document pastes under a command whose result is empty.
    /// </summary>
    private const string NoOutput = "    # no output, exit 1";

    /// <summary>
    /// The guard the conflict floor paragraph names as holding the answer up.
    /// </summary>
    private const string FloorGuard = "ConflictFloorTests";

    /// <summary>
    /// The command the paragraph under the list rests its absence on, with the
    /// token its own grep matches. The paste states the absence and this states
    /// that the absence still holds, so the sentence and the tree cannot drift
    /// apart the way the list above them did.
    /// </summary>
    private static readonly (string Command, string Token) _absence =
        ("    git grep -In \"Payload\" -- 'Jellyfin.Plugin.MetadataSync/'", "Payload");

    /// <summary>
    /// The command the conflict floor paragraph pastes, the file it names and
    /// the site it says the answer is written at. The document hands a reader
    /// the command; this runs it over the copied sources and asks for exactly
    /// one answer, because a second floor is a second default.
    /// </summary>
    private static readonly (string Command, string File, string Site) _floor =
    (
        "    git grep -In \"ConflictOutcome.Refuse, rule: null\" -- 'Jellyfin.Plugin.MetadataSync/Conflicts/ConflictResolver.cs'",
        "Conflicts/ConflictResolver.cs",
        "ConflictOutcome.Refuse, rule: null");

    /// <summary>
    /// The document, copied to the output for the reason the field register is:
    /// walking up from the test binary answers a different question on a
    /// machine where the tests run from somewhere else.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "threat-model.md");

    /// <summary>
    /// The plugin's sources, copied beside the test binary by the test project
    /// file, under the folders they are declared in.
    /// </summary>
    private static readonly string _sources = Path.Combine(AppContext.BaseDirectory, "plugin-sources");

    /// <summary>
    /// Every defence the entry declares as being in the tree is a file that is
    /// in it. This is the direction that catches a defence deleted or renamed
    /// while the document goes on naming it.
    /// </summary>
    [Fact]
    public void EveryDefenceDeclaredInTheTreeIsAFileInTheTree()
    {
        var missing = InTheTreePaths()
            .Where(path => !File.Exists(SourcePath(path)))
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// Every path an entry names starts at the plugin project, so a list that
    /// began naming files in the suite or in another project is refused rather
    /// than read as one of this plugin's.
    /// </summary>
    [Fact]
    public void EveryDefenceDeclaredInTheTreeNamesAPathInsideThePluginProject()
    {
        var outside = RawEntries()
            .Where(entry => string.Equals(entry.Head, InTheTree, StringComparison.Ordinal))
            .Select(entry => entry.Quoted)
            .Where(path => !path.StartsWith(ProjectPrefix, StringComparison.Ordinal))
            .ToList();

        Assert.Empty(outside);
    }

    /// <summary>
    /// Every defence the entry declares as owed names an issue and no file.
    /// Without this leg a path could appear beside the word owed and be read by
    /// nothing, which is the state the defect this guard was written for was
    /// in: a defence in the tree, described as absent, with nobody the wiser.
    /// </summary>
    [Fact]
    public void EveryDefenceDeclaredOwedNamesAnIssueAndNoFile()
    {
        var wrong = RawEntries()
            .Where(entry => string.Equals(entry.Head, Owed, StringComparison.Ordinal))
            .Where(entry => entry.Quoted.Length > 0 || !NamesAnIssue(entry.Text))
            .Select(entry => entry.Text)
            .ToList();

        Assert.Empty(wrong);
    }

    /// <summary>
    /// The fence is still there and both states are declared inside it. Without
    /// this leg a renamed comment, or a list somebody emptied, would leave
    /// every sweep above green over nothing, which is the direction that fails
    /// open.
    /// </summary>
    [Fact]
    public void TheListTheSweepsReadIsStillThere()
    {
        var lines = Lines(_document);

        Assert.Contains(ListOpens, lines, StringComparer.Ordinal);
        Assert.Contains(ListCloses, lines, StringComparer.Ordinal);
        Assert.NotEmpty(InTheTreePaths());
        Assert.Contains(RawEntries(), entry => string.Equals(entry.Head, Owed, StringComparison.Ordinal));
    }

    /// <summary>
    /// The absence the paragraph under the list rests on is re-derived rather
    /// than read. The paragraph pastes the command with an empty result under
    /// it, and both the paste and the emptiness are checked, so a payload type
    /// arriving without the paragraph being rewritten reddens here.
    /// </summary>
    [Fact]
    public void TheAbsenceTheEntryRestsOnIsStillAnAbsence()
    {
        var lines = Lines(_document);
        var head = lines.IndexOf(_absence.Command);

        Assert.True(head >= 0, "The entry no longer carries the command: " + _absence.Command.Trim());
        Assert.Equal(NoOutput, lines[head + 1]);
        Assert.Empty(SourcesNaming(_absence.Token));
    }

    /// <summary>
    /// The conflict floor paragraph carries its command, and the site it says
    /// the answer is written at is in the file it names, exactly once. A second
    /// answer under the table is a second default, which is the one change that
    /// would make every rule above it advisory.
    /// </summary>
    [Fact]
    public void TheConflictFloorIsWrittenWhereTheParagraphSaysItIs()
    {
        var lines = Lines(_document);

        Assert.Contains(_floor.Command, lines, StringComparer.Ordinal);

        var resolver = SourcePath(_floor.File);

        Assert.True(File.Exists(resolver), "The paragraph names a file the plugin no longer has: " + _floor.File);

        var sites = Lines(resolver)
            .Count(line => line.Contains(_floor.Site, StringComparison.Ordinal));

        Assert.Equal(1, sites);
    }

    /// <summary>
    /// The guard the conflict floor paragraph names is in the suite and carries
    /// tests. Without this the paragraph could go on crediting a guard somebody
    /// had deleted, which is the same failure one file over.
    /// </summary>
    [Fact]
    public void TheGuardTheConflictFloorParagraphNamesIsInTheSuite()
    {
        var lines = Lines(_document);

        Assert.Contains(lines, line => line.Contains(Quote + FloorGuard + Quote, StringComparison.Ordinal));

        var guard = typeof(ThreatModelTests).Assembly
            .GetTypes()
            .SingleOrDefault(type => string.Equals(type.Name, FloorGuard, StringComparison.Ordinal));

        Assert.NotNull(guard);
        Assert.Contains(
            guard!.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.GetCustomAttributes()
                .Any(attribute => attribute is FactAttribute or TheoryAttribute));
    }

    /// <summary>
    /// The paths the entries declare as being in the tree, with the project
    /// prefix removed.
    /// </summary>
    /// <returns>The paths, relative to the plugin project.</returns>
    private static List<string> InTheTreePaths() =>
        RawEntries()
            .Where(entry => string.Equals(entry.Head, InTheTree, StringComparison.Ordinal))
            .Select(entry => entry.Quoted)
            .Where(path => path.StartsWith(ProjectPrefix, StringComparison.Ordinal))
            .Select(path => path[ProjectPrefix.Length..])
            .ToList();

    /// <summary>
    /// The entries inside the fence, each with the state it opens with, the
    /// first quoted value on it and the rest of the line after the state.
    /// </summary>
    /// <returns>The entries.</returns>
    private static List<(string Head, string Quoted, string Text)> RawEntries()
    {
        var lines = Lines(_document);
        var opens = lines.IndexOf(ListOpens);
        var closes = lines.IndexOf(ListCloses);

        if (opens < 0 || closes < opens)
        {
            return new List<(string Head, string Quoted, string Text)>();
        }

        var entries = new List<(string Head, string Quoted, string Text)>();

        foreach (var line in lines.Skip(opens + 1).Take(closes - opens - 1))
        {
            var head = new[] { InTheTree, Owed }
                .FirstOrDefault(candidate => line.StartsWith(candidate, StringComparison.Ordinal));

            if (head is null)
            {
                continue;
            }

            entries.Add((head, FirstQuoted(line, head.Length), line[head.Length..]));
        }

        return entries;
    }

    /// <summary>
    /// The first backtick-quoted value on a line after an offset, or the empty
    /// string where the line quotes nothing.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="from">Where to start looking.</param>
    /// <returns>The quoted value.</returns>
    private static string FirstQuoted(string line, int from)
    {
        var opens = line.IndexOf(Quote, from, StringComparison.Ordinal);

        if (opens < 0)
        {
            return string.Empty;
        }

        var closes = line.IndexOf(Quote, opens + 1, StringComparison.Ordinal);

        return closes < 0 ? string.Empty : line[(opens + 1)..closes];
    }

    /// <summary>
    /// Whether a line names an issue, as a hash followed by at least one digit.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>Whether it names one.</returns>
    private static bool NamesAnIssue(string line)
    {
        var hash = line.IndexOf('#', StringComparison.Ordinal);

        return hash >= 0 && hash + 1 < line.Length && char.IsAsciiDigit(line[hash + 1]);
    }

    /// <summary>
    /// Where a path relative to the plugin project lands beside the test
    /// binary.
    /// </summary>
    /// <param name="path">A path relative to the plugin project.</param>
    /// <returns>The copied file.</returns>
    private static string SourcePath(string path) =>
        DeclaredPath.Resolve("docs/threat-model.md", _sources, path);

    /// <summary>
    /// A copied source read back as the document would name it, relative to the
    /// plugin project and with one separator whichever platform it ran on.
    /// </summary>
    /// <param name="path">The copied file.</param>
    /// <returns>The path relative to the plugin project.</returns>
    private static string Relative(string path) =>
        Path.GetRelativePath(_sources, path).Replace('\\', '/');

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
    /// The sources naming a token, reported by the path the document would name
    /// them by, so a failure says which file arrived rather than only that one
    /// did.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The paths, relative to the plugin project.</returns>
    private static IReadOnlyList<string> SourcesNaming(string token) =>
        SourceFiles()
            .Where(path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
            .Select(path => ProjectPrefix + Relative(path))
            .ToList();

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
