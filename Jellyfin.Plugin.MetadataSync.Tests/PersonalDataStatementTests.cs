using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Re-runs the one command <c>docs/personal-data.md</c> pastes the output of.
///
/// That document argues that this plugin has one destination by showing what it
/// depends on, and the showing is a pasted run of a grep over the plugin's
/// project file. A paste is a claim about another artefact made from the nearest
/// thing to hand, and it stops being true the moment the artefact moves without
/// anybody re-running it.
///
/// It had. The block described the project as it stood while one server line was
/// compiled here, the project gained a second set of references under a second
/// target, and the paste stayed. What it then showed a reader was a shorter
/// dependency list than the plugin has, in the file whose whole argument is that
/// the list is short.
///
/// So the block is derived here rather than read. What is compared is the output
/// the command produces from the project file, line numbers included, against
/// the lines the document carries under its marker.
///
/// What this does not reach: every other paste in that document, and every
/// sentence around this one. A count written beside the block would be a second
/// answer this does not check, which is why the document carries none.
/// </summary>
public class PersonalDataStatementTests
{
    /// <summary>
    /// The document, copied to the output for the reason the field register is:
    /// walking up from the test binary answers a different question on a machine
    /// where the tests run from somewhere else.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "personal-data.md");

    /// <summary>
    /// The plugin's project file, which is the artefact the paste is about. The
    /// same copy <c>ManifestTests</c> reads, and the project rather than the
    /// built assembly, because what the document shows is what was declared.
    /// </summary>
    private static readonly string _project = Path.Combine(AppContext.BaseDirectory, "Jellyfin.Plugin.MetadataSync.csproj");

    /// <summary>
    /// The comment that says which block below it is a run rather than prose.
    /// </summary>
    private const string Marker = "<!-- run against Jellyfin.Plugin.MetadataSync.csproj: package references -->";

    /// <summary>
    /// What the pasted command matches on, spelled as the command spells it.
    /// </summary>
    private const string Match = "PackageReference Include";

    /// <summary>
    /// The indent an indented code block carries in this document.
    /// </summary>
    private const string Indent = "    ";

    /// <summary>
    /// The command the block pastes the output of, so the document shows the
    /// reader how to reproduce it and this shows that it still reproduces.
    /// </summary>
    private const string Command =
        "git show origin/master:Jellyfin.Plugin.MetadataSync/Jellyfin.Plugin.MetadataSync.csproj | grep -n 'PackageReference Include'";

    /// <summary>
    /// The output the document carries is the output the command produces. Both
    /// directions: a reference added to the project with no line added here
    /// fails, and a line here the project does not produce fails too.
    /// </summary>
    [Fact]
    public void ThePastedProjectReferencesAreWhatTheProjectDeclares()
    {
        Assert.Equal(Produced(), Pasted());
    }

    /// <summary>
    /// The reading finds a block with the command at the head of it and output
    /// under it. Without this leg a marker that had been renamed, or a block
    /// that had lost its command line, would leave the comparison above green
    /// over two empty lists, which is the direction that fails open.
    /// </summary>
    [Fact]
    public void TheBlockThePasteLivesInIsStillThere()
    {
        Assert.Contains(Marker, Lines(_document), StringComparer.Ordinal);
        Assert.Contains(Indent + Command, Lines(_document), StringComparer.Ordinal);
        Assert.NotEmpty(Produced());
        Assert.NotEmpty(Pasted());
    }

    /// <summary>
    /// What the command produces from the project file: every matching line,
    /// numbered from one, in the form <c>grep -n</c> prints.
    /// </summary>
    /// <returns>The output lines.</returns>
    private static List<string> Produced() =>
        Lines(_project)
            .Select((text, index) => (Text: text, Number: index + 1))
            .Where(l => l.Text.Contains(Match, StringComparison.Ordinal))
            .Select(l => l.Number.ToString(CultureInfo.InvariantCulture) + ":" + l.Text)
            .ToList();

    /// <summary>
    /// What the document carries under the marker: the indented lines after the
    /// command line, to the first line that is not part of the block.
    /// </summary>
    /// <returns>The pasted lines, with the block indent removed.</returns>
    private static List<string> Pasted()
    {
        var lines = Lines(_document);
        var head = lines.IndexOf(Indent + Command);

        if (head < 0)
        {
            return new List<string>();
        }

        var pasted = new List<string>();

        foreach (var line in lines.Skip(head + 1))
        {
            if (!line.StartsWith(Indent, StringComparison.Ordinal))
            {
                break;
            }

            pasted.Add(line[Indent.Length..]);
        }

        return pasted;
    }

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
            .Select(l => l.TrimEnd())
            .ToList();
}
