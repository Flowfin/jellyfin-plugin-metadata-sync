using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds <c>docs/supported-servers.md</c> to what the build actually produces.
///
/// The document tells an operator which server line the package they are about
/// to install is for, and every number in it is also written somewhere the
/// build reads: the runtime and the target ABI in the manifest, the server
/// package in the plugin project. A document restating those is a second
/// answer, and the failure it produces is silent in the worst direction. An
/// operator installs on the strength of a row that was true when it was typed,
/// the manifest has moved since, and the first thing that says otherwise is a
/// plugin the server marks as not supported after a restart.
///
/// What is guarded is the table and nothing else. The document's account of
/// how the server treats a package built for another line is a reading of a
/// Jellyfin checkout, there is no server here to re-derive it from, and the
/// document says so in its own last section rather than leaving a reader to
/// assume the whole file is checked.
/// </summary>
public class SupportedServersTests
{
    /// <summary>
    /// A row of the table, with the cells trimmed of the spacing that makes
    /// the source readable.
    /// </summary>
    /// <param name="Line">The server line, as major and minor.</param>
    /// <param name="Runtime">The runtime that line needs.</param>
    /// <param name="Built">Whether this repository packages an artefact for it.</param>
    /// <param name="Abi">The target ABI that package declares.</param>
    /// <param name="CompiledAgainst">The server package that package was compiled against.</param>
    private sealed record Row(string Line, string Runtime, string Built, string Abi, string CompiledAgainst);

    /// <summary>
    /// The cell a row uses to say a package for that line is produced here.
    /// </summary>
    private const string Built = "yes";

    /// <summary>
    /// The manifest declares one <c>targetAbi</c> and one <c>framework</c>, so
    /// it can account for exactly one built artefact. A table claiming two is
    /// offering an operator a package that is not made, which is the direction
    /// this fails in when somebody edits the document ahead of the build.
    ///
    /// This leg is also what will redden the day a second artefact lands, and
    /// that is the point of it rather than a cost: the document cannot go on
    /// saying one line is unbuilt while the build produces it.
    /// </summary>
    [Fact]
    public void ExactlyOneLineIsClaimedAsBuiltHere()
    {
        var claimed = Table().Where(r => string.Equals(r.Built, Built, StringComparison.Ordinal)).ToList();

        Assert.True(
            claimed.Count == 1,
            "docs/supported-servers.md claims " + claimed.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " built artefacts and build.yaml declares one targetAbi and one framework.");
    }

    /// <summary>
    /// The runtime in the built row against both places the build states it.
    /// The manifest tells the server which runtime to expect and the project
    /// decides which one is produced, so a document agreeing with one of them
    /// and not the other is still wrong for whichever reader trusts it.
    /// </summary>
    [Fact]
    public void TheBuiltRowNamesTheRuntimeTheManifestAndTheProjectAgreeOn()
    {
        var row = BuiltRow();

        Assert.Equal(ManifestField("framework"), row.Runtime);
        Assert.Contains(row.Runtime, PluginProjectFile.TargetFrameworks(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Every line in the table, packaged or not, against the frameworks the
    /// project actually builds. The leg above covers the one row a package
    /// exists for; this one covers the other, which is the row a reader turns
    /// to when they want to know whether their line is compiled here at all.
    /// </summary>
    /// <remarks>
    /// It is a set comparison in both directions on purpose. A line dropped
    /// from the build and left in the document offers a reader a runtime
    /// nothing is compiled for, and a target added to the build and left out of
    /// the document hides the line that arrived.
    /// </remarks>
    [Fact]
    public void EveryLineInTheTableIsARuntimeTheProjectBuilds()
    {
        var declared = PluginProjectFile.TargetFrameworks().Order(StringComparer.Ordinal).ToList();
        var tabled = Table().Select(r => r.Runtime).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(declared, tabled);
    }

    /// <summary>
    /// Each line's runtime against the server packages the project references
    /// for it. A conditional reference wired to the wrong framework compiles
    /// the newer line against the older server and every leg above stays green,
    /// because each cell still agrees with the file it was copied from.
    /// </summary>
    [Fact]
    public void EachLinesRuntimeCarriesThatLinesServerPackages()
    {
        foreach (var row in Table())
        {
            var controller = PluginProjectFile.PackageVersion("Jellyfin.Controller", row.Runtime);
            var model = PluginProjectFile.PackageVersion("Jellyfin.Model", row.Runtime);

            Assert.Equal(controller, model);
            Assert.Equal(row.Line, LineOf(controller));
        }
    }

    /// <summary>
    /// The target ABI in the built row against the manifest. This is the
    /// number the document's own second section is about, so a stale copy of
    /// it makes the explanation beside it describe a package that does not
    /// exist.
    /// </summary>
    [Fact]
    public void TheBuiltRowNamesTheTargetAbiTheManifestDeclares()
    {
        Assert.Equal(ManifestField("targetAbi"), BuiltRow().Abi);
    }

    /// <summary>
    /// The server package in the built row against the plugin project, read
    /// from the project rather than from the compiled assembly for the reason
    /// the manifest legs are: the project is what was declared, and the two
    /// diverge exactly when the document is the thing that is wrong.
    /// </summary>
    [Fact]
    public void TheBuiltRowNamesTheServerPackageTheProjectReferences()
    {
        var row = BuiltRow();
        var declared = PluginProjectFile.PackageVersion("Jellyfin.Controller", row.Runtime);

        Assert.Equal("Jellyfin.Controller " + declared, row.CompiledAgainst);
    }

    /// <summary>
    /// The line a row is about has to be the line its own numbers are on. A
    /// row headed 10.11 carrying an ABI of 12.0, or a server package from
    /// another line, is the mistake that survives every leg above: each cell
    /// agrees with the file it was copied from and the row as a whole says
    /// something none of them said.
    /// </summary>
    [Fact]
    public void TheBuiltRowsNumbersAreOnTheLineTheRowIsAbout()
    {
        var row = BuiltRow();
        var abi = Version.Parse(row.Abi);

        Assert.Equal(row.Line, abi.Major + "." + abi.Minor);
        Assert.Equal(row.Line, LineOf(row.CompiledAgainst.Split(' ').Last()));
    }

    /// <summary>
    /// A row for a line nothing is packaged for carries no ABI and no package,
    /// because there is no package for either to belong to. A number left in
    /// those cells reads as something an operator can install, and the line
    /// being compiled here does not make one.
    /// </summary>
    [Fact]
    public void ALineWithNoArtefactCarriesNoAbiAndNoPackage()
    {
        foreach (var row in Table().Where(r => !string.Equals(r.Built, Built, StringComparison.Ordinal)))
        {
            Assert.Equal("none", row.Abi);
            Assert.Equal("nothing", row.CompiledAgainst);
        }
    }

    /// <summary>
    /// The near miss for the reader rather than for the rule. A table read out
    /// of a document that does not carry one comes back empty, and an empty
    /// table satisfies every leg above by having nothing to disagree with. So
    /// the reader is held to finding both lines, and a document that lost its
    /// table fails here instead of passing everywhere.
    /// </summary>
    [Fact]
    public void TheTableIsFoundAndCarriesBothSupportedLines()
    {
        var lines = Table().Select(r => r.Line).ToList();

        Assert.Equal(2, lines.Count);
        Assert.Equal(lines, lines.Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// Reads the one table in the document, by shape rather than by position.
    /// A row is five cells between pipes; the header and the dashes under it
    /// are dropped by name and by shape respectively, so a column renamed
    /// without this file being touched leaves the header behind as a row and
    /// reddens rather than passing quietly.
    /// </summary>
    /// <returns>The rows of the table, in the order the document carries them.</returns>
    private static IReadOnlyList<Row> Table()
    {
        var document = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "supported-servers.md"));
        var rows = new List<Row>();

        foreach (var line in document.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (!line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();
            if (cells.Count != 5)
            {
                continue;
            }

            if (string.Equals(cells[0], "Server line", StringComparison.Ordinal)
                || cells.All(c => c.Length > 0 && c.All(ch => ch == '-')))
            {
                continue;
            }

            rows.Add(new Row(cells[0], cells[1], cells[2], cells[3], cells[4]));
        }

        return rows;
    }

    private static Row BuiltRow()
    {
        var row = Table().FirstOrDefault(r => string.Equals(r.Built, Built, StringComparison.Ordinal));

        Assert.True(row is not null, "docs/supported-servers.md claims no built artefact at all.");
        return row!;
    }

    /// <summary>
    /// The server line a package version is on, as the table writes a line.
    /// A prerelease is read for its line and not for its suffix, because the
    /// 12.0 line ships as a release candidate and <c>Version</c> refuses one.
    /// </summary>
    private static string LineOf(string packageVersion)
    {
        var numbers = packageVersion.Split('-')[0].Split('.');

        Assert.True(numbers.Length >= 2, "The version " + packageVersion + " names no major and minor.");
        return numbers[0] + "." + numbers[1];
    }

    private static string ManifestField(string name)
    {
        var manifest = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "build.yaml"))
            .Split('\n').Select(l => l.TrimEnd('\r'));
        var field = new Regex("^" + Regex.Escape(name) + ":[ \t]*\"([^\"]*)\"[ \t]*$");

        var match = manifest.Select(l => field.Match(l)).FirstOrDefault(m => m.Success);

        Assert.True(match is not null, "build.yaml declares no quoted '" + name + "' field.");
        return match!.Groups[1].Value;
    }

}
