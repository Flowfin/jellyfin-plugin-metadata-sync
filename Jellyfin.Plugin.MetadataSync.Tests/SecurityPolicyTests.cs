using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the opening section of <c>SECURITY.md</c> against the tree it ships
/// with.
///
/// That section exists so a reader can tell a defence that is running from one
/// that is owed, and it had stopped being able to do that. It pasted a list of
/// eight files while the plugin held sixty-four, said the field register was
/// planned and not built while the planner asks it first, and concluded that
/// there was no sync in the tree. It missed in the direction that understates
/// the plugin, in the file whose own next paragraph says a policy reading as a
/// description of a working system is a claim about code nobody has written.
///
/// It was the fourth document in this repository found describing a tree that
/// had moved under it, and every one of the four was found by somebody working
/// on a neighbouring change. Nothing rendered any of them and nothing read
/// them, so the interval between a paragraph going stale and somebody meeting
/// it was bounded by nothing. This is that bound for one section of one file.
///
/// What it reads, in the two directions that fail differently. A part the
/// section names as being in the tree has to be a file in the tree, so a type
/// deleted or renamed reddens here rather than leaving the policy claiming it.
/// And every folder the plugin's sources are organised into has to be named by
/// one of those parts or declared below as not being one, so a subsystem that
/// lands without the policy noticing reddens too. That second direction is the
/// one this defect actually took: nothing was removed, subsystems arrived, and
/// the sentence stayed.
///
/// What it cannot reach, stated rather than left to be assumed. It judges the
/// paths in the list and never the sentence beside each one, so a part
/// described wrongly passes. It says nothing about the rest of the file: the
/// reporting route, the scope sections and the residual paragraphs are prose
/// that no reading of this tree judges, and whether a sentence about meaning is
/// true is what the review is for. And a folder named by a part is satisfied by
/// any one file in it, so a second subsystem added inside an existing folder is
/// not seen.
/// </summary>
public class SecurityPolicyTests
{
    /// <summary>
    /// The prefix a path in the list carries, which is the project directory.
    /// It is written out rather than trimmed to the last two segments, so a
    /// path naming some other project is refused rather than silently read as
    /// one of this plugin's.
    /// </summary>
    private const string ProjectPrefix = "Jellyfin.Plugin.MetadataSync/";

    /// <summary>
    /// The comment that opens the fence around the list, so a reading of it is
    /// a reading of what the section declares rather than of every bullet in
    /// the file.
    /// </summary>
    private const string ListOpens = "<!-- the parts in the tree: one per line, the file first, read by SecurityPolicyTests -->";

    /// <summary>
    /// The comment that closes the fence.
    /// </summary>
    private const string ListCloses = "<!-- end of the parts -->";

    /// <summary>
    /// The indent an indented code block carries in this document.
    /// </summary>
    private const string Indent = "    ";

    /// <summary>
    /// What the section pastes under each command whose result is empty.
    /// </summary>
    private const string NoOutput = "    # no output, exit 1";

    /// <summary>
    /// The command the pasted file list was replaced by.
    /// </summary>
    private const string ListCommand = "    git ls-tree -r --name-only origin/master -- " + ProjectPrefix;

    /// <summary>
    /// The policy, copied to the output for the reason the field register is:
    /// walking up from the test binary answers a different question on a
    /// machine where the tests run from somewhere else.
    /// </summary>
    private static readonly string _policy = Path.Combine(AppContext.BaseDirectory, "SECURITY.md");

    /// <summary>
    /// The plugin's sources, copied beside the test binary by the test project
    /// file, under the folders they are declared in.
    /// </summary>
    private static readonly string _sources = Path.Combine(AppContext.BaseDirectory, "plugin-sources");

    /// <summary>
    /// The two commands the section pastes an empty result under, each with the
    /// tokens its own grep matches on. The paste states the absence and this
    /// states that the absence still holds, so the sentence and the tree cannot
    /// drift apart the way the list above them did.
    /// </summary>
    private static readonly (string Command, string[] Tokens)[] _absences =
    {
        ("    git grep -In \"IScheduledTask\" -- 'Jellyfin.Plugin.MetadataSync/'", new[] { "IScheduledTask" }),
        ("    git grep -In \"ControllerBase\\|ApiController\" -- 'Jellyfin.Plugin.MetadataSync/'", new[] { "ControllerBase", "ApiController" }),
    };

    /// <summary>
    /// The folders of the plugin that are not a part of what the policy
    /// describes, with the reason each is outside. Declared here rather than
    /// left out of the sweep, because a folder skipped in silence is the same
    /// hole as a policy that never mentioned it.
    /// </summary>
    private static readonly Dictionary<string, string> _notAPart = new(StringComparer.Ordinal)
    {
        ["Properties"] = "assembly attributes, which are build output rather than a decision this plugin makes",
        [""] = "the entry point and the service registration, which every plugin has and which the section names in its own sentences",
    };

    /// <summary>
    /// Every path the section names as being in the tree is a file that is in
    /// it. This is the direction that catches a part deleted or renamed while
    /// the policy goes on claiming it.
    /// </summary>
    [Fact]
    public void EveryPartTheSectionNamesIsAFileInTheTree()
    {
        var missing = Parts()
            .Where(path => !File.Exists(SourcePath(path)))
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// Every folder the plugin's sources are organised into is named by a part
    /// or is declared as not being one. This is the direction the defect this
    /// guard was written for actually took: nothing was removed, several
    /// subsystems arrived, and the section went on describing the tree from
    /// before them.
    /// </summary>
    [Fact]
    public void EveryFolderOfPluginSourcesIsNamedOrDeclaredNotToBeAPart()
    {
        var named = Parts()
            .Select(FolderOf)
            .ToHashSet(StringComparer.Ordinal);

        var unaccounted = Folders()
            .Where(folder => !named.Contains(folder) && !_notAPart.ContainsKey(folder))
            .ToList();

        Assert.Empty(unaccounted);
    }

    /// <summary>
    /// A folder declared as not being a part is one the plugin still has.
    /// Without this the declarations rot into a set that excuses folders nobody
    /// has, and the sweep above passes for the wrong reason.
    /// </summary>
    [Fact]
    public void EveryFolderDeclaredNotToBeAPartIsOneThePluginHas()
    {
        var folders = Folders().ToHashSet(StringComparer.Ordinal);

        var gone = _notAPart.Keys
            .Where(folder => !folders.Contains(folder))
            .ToList();

        Assert.Empty(gone);
    }

    /// <summary>
    /// The two sentences about what is absent are re-derived rather than read.
    /// The section pastes each command with an empty result under it, and both
    /// the paste and the emptiness are checked, so a pass or a controller that
    /// arrives without the paragraph being rewritten reddens here.
    /// </summary>
    [Fact]
    public void TheAbsencesTheSectionRestsOnAreStillAbsences()
    {
        var lines = Lines(_policy);

        foreach (var (command, tokens) in _absences)
        {
            var head = lines.IndexOf(command);

            Assert.True(head >= 0, "The section no longer carries the command: " + command.Trim());
            Assert.Equal(NoOutput, lines[head + 1]);
            Assert.Empty(SourcesNaming(tokens));
        }
    }

    /// <summary>
    /// The file list is not pasted back under the command that replaced it. A
    /// paste of it goes stale on every landing, which is what this whole guard
    /// is for, and re-adding it is the repair somebody reaches for who thinks a
    /// reader wants the list without leaving the page.
    /// </summary>
    [Fact]
    public void TheFileListIsNotPastedBackUnderTheCommand()
    {
        var lines = Lines(_policy);
        var head = lines.IndexOf(ListCommand);

        Assert.True(head >= 0, "The section no longer carries the command the file list was replaced by.");

        var pasted = lines
            .Skip(head + 1)
            .TakeWhile(line => line.StartsWith(Indent, StringComparison.Ordinal))
            .ToList();

        Assert.Empty(pasted);
    }

    /// <summary>
    /// The fence is still there and the list inside it is not empty. Without
    /// this leg a renamed comment, or a list somebody emptied, would leave every
    /// sweep above green over nothing, which is the direction that fails open.
    /// </summary>
    [Fact]
    public void TheListTheSweepsReadIsStillThere()
    {
        var lines = Lines(_policy);

        Assert.Contains(ListOpens, lines, StringComparer.Ordinal);
        Assert.Contains(ListCloses, lines, StringComparer.Ordinal);
        Assert.NotEmpty(Parts());
        Assert.NotEmpty(Folders());
    }

    /// <summary>
    /// Every path a part names starts at the plugin project, so a list that
    /// began naming files in the suite or in another project is refused rather
    /// than read as one of this plugin's.
    /// </summary>
    [Fact]
    public void EveryPartNamesAPathInsideThePluginProject()
    {
        var outside = RawParts()
            .Where(path => !path.StartsWith(ProjectPrefix, StringComparison.Ordinal))
            .ToList();

        Assert.Empty(outside);
    }

    /// <summary>
    /// The paths the fenced list declares, with the project prefix removed.
    /// </summary>
    /// <returns>The paths, relative to the plugin project.</returns>
    private static List<string> Parts() =>
        RawParts()
            .Where(path => path.StartsWith(ProjectPrefix, StringComparison.Ordinal))
            .Select(path => path[ProjectPrefix.Length..])
            .ToList();

    /// <summary>
    /// The first quoted value on each bullet inside the fence, as the document
    /// spells it.
    /// </summary>
    /// <returns>The paths.</returns>
    private static List<string> RawParts()
    {
        var lines = Lines(_policy);
        var opens = lines.IndexOf(ListOpens);
        var closes = lines.IndexOf(ListCloses);

        if (opens < 0 || closes < opens)
        {
            return new List<string>();
        }

        var paths = new List<string>();
        var head = "- " + Quote;

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
    /// The folders the plugin's sources are declared in, with the empty string
    /// standing for the project root.
    /// </summary>
    /// <returns>The folders, one entry each.</returns>
    private static List<string> Folders() =>
        SourceFiles()
            .Select(path => FolderOf(Relative(path)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(folder => folder, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The folder a path is in, or the empty string where it is at the root.
    /// </summary>
    /// <param name="path">A path relative to the plugin project.</param>
    /// <returns>The folder.</returns>
    private static string FolderOf(string path)
    {
        var cut = path.IndexOf('/', StringComparison.Ordinal);

        return cut < 0 ? string.Empty : path[..cut];
    }

    /// <summary>
    /// Where a path relative to the plugin project lands beside the test
    /// binary.
    /// </summary>
    /// <param name="path">A path relative to the plugin project.</param>
    /// <returns>The copied file.</returns>
    private static string SourcePath(string path) =>
        Path.Combine(_sources, path.Replace('/', Path.DirectorySeparatorChar));

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
    /// The sources naming any of a set of tokens, reported by the path the
    /// document would name them by, so a failure says which file arrived rather
    /// than only that one did.
    /// </summary>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The paths, relative to the plugin project.</returns>
    private static IReadOnlyList<string> SourcesNaming(IReadOnlyCollection<string> tokens) =>
        SourceFiles()
            .Where(path =>
            {
                var text = File.ReadAllText(path);

                return tokens.Any(token => text.Contains(token, StringComparison.Ordinal));
            })
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
