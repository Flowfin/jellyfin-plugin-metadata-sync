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
    /// A row of the version band table.
    /// </summary>
    /// <param name="Line">The server line, as major and minor.</param>
    /// <param name="Major">The major version number that line's packages carry.</param>
    /// <param name="Manifest">The manifest that declares that line's package.</param>
    /// <param name="Distance">Where that manifest's version sits in the band.</param>
    private sealed record Band(string Line, int Major, string Manifest, string Distance);

    /// <summary>
    /// A manifest whose version is under its band's major, which is where a line
    /// sits before its first release reaches the band it was given.
    /// </summary>
    private const string BelowTheBand = "below the band";

    /// <summary>
    /// A manifest carrying its band's major and nothing else, which is the first
    /// version that line can publish inside its band.
    /// </summary>
    private const string AtTheFootOfTheBand = "at the foot of the band";

    /// <summary>
    /// A manifest carrying its band's major and something after it.
    /// </summary>
    private const string InsideTheBand = "inside the band";

    /// <summary>
    /// Each manifest declares one <c>targetAbi</c> and one <c>framework</c>, so
    /// it accounts for exactly one built artefact. A row claiming a package no
    /// manifest declares offers an operator something that is not made; a
    /// manifest with no row claiming it hides a package that is.
    /// </summary>
    [Fact]
    public void EveryManifestHasOneRowClaimingItAndNoRowClaimsMore()
    {
        var claimed = Table().Where(r => string.Equals(r.Built, Built, StringComparison.Ordinal)).ToList();
        var manifests = ManifestFile.Names();

        Assert.Equal(
            manifests.Select(m => ManifestFile.Field(m, "targetAbi")).Order(StringComparer.Ordinal).ToList(),
            claimed.Select(r => r.Abi).Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The runtime in each built row against both places the build states it.
    /// The manifest tells the server which runtime to expect and the project
    /// decides which one is produced, so a document agreeing with one of them
    /// and not the other is still wrong for whichever reader trusts it.
    /// </summary>
    [Fact]
    public void EveryBuiltRowNamesTheRuntimeItsManifestAndTheProjectAgreeOn()
    {
        foreach (var row in BuiltRows())
        {
            Assert.Equal(ManifestFile.Field(ManifestFor(row), "framework"), row.Runtime);
            Assert.Contains(row.Runtime, PluginProjectFile.TargetFrameworks(), StringComparer.Ordinal);
        }
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
    /// The server package in each built row against the plugin project, read
    /// from the project rather than from the compiled assembly for the reason
    /// the manifest legs are: the project is what was declared, and the two
    /// diverge exactly when the document is the thing that is wrong.
    /// </summary>
    [Fact]
    public void EveryBuiltRowNamesTheServerPackageTheProjectReferences()
    {
        foreach (var row in BuiltRows())
        {
            var declared = PluginProjectFile.PackageVersion("Jellyfin.Controller", row.Runtime);

            Assert.Equal("Jellyfin.Controller " + declared, row.CompiledAgainst);
        }
    }

    /// <summary>
    /// The line a row is about has to be the line its own numbers are on. A
    /// row headed 10.11 carrying an ABI of 12.0, or a server package from
    /// another line, is the mistake that survives every leg above: each cell
    /// agrees with the file it was copied from and the row as a whole says
    /// something none of them said.
    /// </summary>
    [Fact]
    public void EveryBuiltRowsNumbersAreOnTheLineTheRowIsAbout()
    {
        foreach (var row in BuiltRows())
        {
            var abi = Version.Parse(row.Abi);

            Assert.Equal(row.Line, abi.Major + "." + abi.Minor);
            Assert.Equal(row.Line, LineOf(row.CompiledAgainst.Split(' ').Last()));
        }
    }

    /// <summary>
    /// The band table names every line the table above does, once each. A line
    /// with no band is a package whose version says nothing about which server
    /// it is for, which is the whole failure the bands exist against.
    /// </summary>
    [Fact]
    public void TheBandTableNamesEverySupportedLineOnce()
    {
        var bands = Bands();

        Assert.Equal(
            Table().Select(r => r.Line).Order(StringComparer.Ordinal).ToList(),
            bands.Select(b => b.Line).Order(StringComparer.Ordinal).ToList());
        Assert.Equal(bands.Select(b => b.Major).Distinct().Count(), bands.Count);
    }

    /// <summary>
    /// Each band names the manifest for its line, and that manifest declares
    /// that line. A band pointing at the other line's file is a table that
    /// reads correctly and describes the wrong package.
    /// </summary>
    [Fact]
    public void EveryBandNamesTheManifestThatDeclaresItsLine()
    {
        foreach (var band in Bands())
        {
            var abi = Version.Parse(ManifestFile.Field(band.Manifest, "targetAbi"));

            Assert.Equal(band.Line, abi.Major + "." + abi.Minor);
        }
    }

    /// <summary>
    /// And the other direction: every manifest the build could read is named by
    /// a band. A manifest added at the root without a band is a package whose
    /// version nothing constrains, and it would be built by a job somebody adds
    /// beside the two that exist.
    /// </summary>
    [Fact]
    public void EveryManifestIsNamedByABand()
    {
        Assert.Equal(
            ManifestFile.Names().Order(StringComparer.Ordinal).ToList(),
            Bands().Select(b => b.Manifest).Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// A manifest's version sits inside its band: at or below its own major,
    /// and above every lower line's.
    /// </summary>
    /// <remarks>
    /// Below its own is allowed and is the state the 10.11 line is in, because
    /// that line's releases start at 0.1.0.0 under the decision on #122 and
    /// 1.0.0.0 waits until a release has been observed in the field. This
    /// paragraph said instead that nothing had been released, which stopped
    /// being true on 2026-09-03 and left the reason for a permitted state
    /// resting on a fact the tracker contradicts. At or below a lower line's is
    /// never allowed, and that is the half
    /// that protects an operator: it is what keeps the newer line's package
    /// from sorting under the older line's on a server that keeps both.
    /// <para>
    /// That second half is the whole of the ordering property rather than a
    /// step towards it, and a leg asserting the ordering separately was written
    /// and taken out again for that reason: it could not be made to fail on its
    /// own. A newer line's version major is above every lower band, and a lower
    /// line's is at or below its own, so the ordering follows and a leg stating
    /// it would only ever redden beside this one.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryManifestsVersionSitsInsideItsBand()
    {
        var bands = Bands();

        foreach (var band in bands)
        {
            var version = Version.Parse(ManifestFile.Field(band.Manifest, "version"));

            Assert.True(
                version.Major <= band.Major,
                band.Manifest + " declares version " + version + ", above the band the " + band.Line + " line is given, which is major " + band.Major + ".");

            foreach (var lower in bands.Where(b => b.Major < band.Major))
            {
                Assert.True(
                    version.Major > lower.Major,
                    band.Manifest + " declares version " + version + ", at or below the band of the " + lower.Line + " line, which is major " + lower.Major + ". A server keeping both entries would take the wrong package.");
            }
        }
    }

    /// <summary>
    /// Each band's last cell says where that manifest's version sits, and it is
    /// derived from the manifest rather than compared with a number typed beside
    /// it.
    /// </summary>
    /// <remarks>
    /// The paragraph under this table used to answer the same question by
    /// restating both versions as literals, and one of them had moved: it said
    /// <c>build.yaml</c> carried 0.1.0.0 while that file carried 0.1.1.0, and
    /// nothing read the sentence. A version is exactly the kind of number a
    /// document should not hold a second copy of, so the number is gone from the
    /// prose and the characterisation is what the document states.
    /// <para>
    /// The set is closed at three, and the leg below holds it so, because a cell
    /// spelled anything else would fall through this comparison as a phrase
    /// nothing derives.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryBandSaysWhereItsManifestsVersionActuallySits()
    {
        foreach (var band in Bands())
        {
            Assert.Equal(DistanceOf(band), band.Distance);
        }
    }

    /// <summary>
    /// The near miss for the cell rather than for the rule. A phrase outside the
    /// closed set describes nothing this file can derive, and the leg above
    /// would refuse it only by accident of which of the three it was compared
    /// against.
    /// </summary>
    [Fact]
    public void EveryBandsDistanceIsOneOfTheThreeThisFileCanDerive()
    {
        var known = new[] { BelowTheBand, AtTheFootOfTheBand, InsideTheBand };

        foreach (var band in Bands())
        {
            Assert.Contains(band.Distance, known, StringComparer.Ordinal);
        }
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
    /// the reader is held to finding both lines, and a document that lost
    /// either table fails here instead of passing everywhere.
    /// </summary>
    [Fact]
    public void BothTablesAreFoundAndCarryBothSupportedLines()
    {
        var lines = Table().Select(r => r.Line).ToList();

        Assert.Equal(2, lines.Count);
        Assert.Equal(lines, lines.Distinct(StringComparer.Ordinal).ToList());

        var banded = Bands().Select(b => b.Line).ToList();

        Assert.Equal(2, banded.Count);
        Assert.Equal(banded, banded.Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// Reads the table of supported lines, which is the five-cell one.
    /// </summary>
    /// <returns>The rows, in the order the document carries them.</returns>
    private static IReadOnlyList<Row> Table()
        => Rows(5).Select(cells => new Row(cells[0], cells[1], cells[2], cells[3], cells[4])).ToList();

    /// <summary>
    /// Reads the rows of a table in the document, by shape rather than by
    /// position. A row is a fixed number of cells between pipes; the header and
    /// the dashes under it are dropped by name and by shape respectively, so a
    /// column renamed without this file being touched leaves the header behind
    /// as a row and reddens rather than passing quietly.
    /// </summary>
    /// <remarks>
    /// The document carries two tables and the width is what tells them apart.
    /// That is a real bound rather than a convenience: two tables of one width
    /// would be read as one, so a third table added here needs a width of its
    /// own or this reader needs replacing.
    /// </remarks>
    /// <param name="width">The number of cells a row of the wanted table has.</param>
    /// <returns>The cells of each row, in the order the document carries them.</returns>
    private static IReadOnlyList<IReadOnlyList<string>> Rows(int width)
    {
        var document = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "supported-servers.md"));
        var rows = new List<IReadOnlyList<string>>();

        foreach (var line in document.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (!line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();
            if (cells.Count != width)
            {
                continue;
            }

            if (string.Equals(cells[0], "Server line", StringComparison.Ordinal)
                || cells.All(c => c.Length > 0 && c.All(ch => ch == '-')))
            {
                continue;
            }

            rows.Add(cells);
        }

        return rows;
    }

    /// <summary>
    /// Reads the version band table, which is four cells wide where the table
    /// above is five, so the reader above skips it and this one skips that.
    /// </summary>
    /// <returns>The bands, in the order the document carries them.</returns>
    private static IReadOnlyList<Band> Bands()
    {
        var bands = new List<Band>();

        foreach (var cells in Rows(4))
        {
            Assert.True(
                int.TryParse(cells[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var major),
                "The version band table in docs/supported-servers.md gives the " + cells[0] + " line the band '" + cells[1] + "', which is not a major version number.");

            bands.Add(new Band(cells[0], major, cells[2], cells[3]));
        }

        Assert.NotEmpty(bands);
        return bands;
    }

    /// <summary>
    /// Where a band's manifest actually carries its version, in the words the
    /// document uses.
    /// </summary>
    /// <param name="band">The band, for its major and its manifest.</param>
    /// <returns>One of the three phrases this file derives.</returns>
    private static string DistanceOf(Band band)
    {
        var version = Version.Parse(ManifestFile.Field(band.Manifest, "version"));

        if (version.Major < band.Major)
        {
            return BelowTheBand;
        }

        return version == new Version(band.Major, 0, 0, 0) ? AtTheFootOfTheBand : InsideTheBand;
    }

    private static IReadOnlyList<Row> BuiltRows()
    {
        var rows = Table().Where(r => string.Equals(r.Built, Built, StringComparison.Ordinal)).ToList();

        Assert.True(rows.Count > 0, "docs/supported-servers.md claims no built artefact at all.");
        return rows;
    }

    /// <summary>
    /// The manifest that declares the package a built row is about, found by
    /// the ABI rather than by position, so a row cannot pass by agreeing with
    /// the other line's file.
    /// </summary>
    /// <param name="row">The built row.</param>
    /// <returns>The manifest file name.</returns>
    private static string ManifestFor(Row row)
    {
        var manifests = ManifestFile.Names()
            .Where(m => string.Equals(ManifestFile.Field(m, "targetAbi"), row.Abi, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            manifests.Count == 1,
            "docs/supported-servers.md claims a package for the " + row.Line + " line at ABI " + row.Abi
                + ", and " + manifests.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " manifests declare it.");

        return manifests[0];
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

}
