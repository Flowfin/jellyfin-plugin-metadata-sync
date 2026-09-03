using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Guards the manifest fields that reach an operator wrong without any build
/// failing: the version the package is stamped with, the runtime the package
/// claims, the server line it claims to install on, and the owner it names.
/// Each was the project template's value at some point, and none of them is
/// checkable against the manifest alone.
/// </summary>
/// <remarks>
/// There is one manifest per supported server line and the legs here walk the
/// set rather than one file, because a field that is right in the manifest
/// somebody edited and wrong in the other is exactly the failure two manifests
/// introduce. <c>docs/supported-servers.md</c> is where the lines and their
/// version bands are declared, and <see cref="SupportedServersTests"/> holds
/// that document against this same set.
/// </remarks>
public class ManifestTests
{
    /// <summary>
    /// The names the packaging tool reads before it reads `build.yaml`, from
    /// the revision `.github/workflows/build.yaml` calls:
    ///
    ///     gh api repos/oddstr13/jellyfin-plugin-repository-manager/contents/jprm/__init__.py?ref=9497a0a499416cc572ed2e07a391d9f943a37b4d --jq '.content' | base64 -d | sed -n '37,45p'
    ///     CONFIG_LOCATIONS = [
    ///         "jprm.yaml",
    ///         ".jprm.yaml",
    ///         ".ci/jprm.yaml",
    ///         ".github/jprm.yaml",
    ///         ".gitlab/jprm.yaml",
    ///         "meta.yaml",
    ///         "build.yaml",
    ///     ]
    /// </summary>
    private static readonly string[] AheadOfTheManifest =
    {
        "jprm.yaml",
        ".jprm.yaml",
        ".ci/jprm.yaml",
        ".github/jprm.yaml",
        ".gitlab/jprm.yaml",
        "meta.yaml",
    };

    /// <summary>
    /// The fields that say what a package is, rather than which server line it
    /// is for. Every manifest here belongs to one plugin, so all of them are
    /// the same in every manifest, and a package differing in any of them is a
    /// second plugin in the catalogue rather than a second build of this one.
    /// </summary>
    private static readonly string[] IdentityFields =
    {
        "name",
        "guid",
        "imageUrl",
        "category",
        "owner",
        "overview",
    };

    /// <summary>
    /// The manifest is the single place a release version is written and the
    /// build reads it from there. A version restated in the build lets a plain
    /// build and a packaged build disagree, and the package then carries a
    /// version the catalogue never showed.
    /// </summary>
    /// <remarks>
    /// The default manifest and no other, because it is the one a build that
    /// stages nothing reads, and this assembly is the product of exactly that
    /// build. A package for another line is stamped from the manifest staged
    /// over it, which is the same read of the same field one file along.
    /// </remarks>
    [Fact]
    public void StampedVersionIsTheVersionTheManifestDeclares()
    {
        var declared = ManifestFile.Field(ManifestFile.Default, "version");
        var stamped = typeof(Plugin).Assembly.GetName().Version;

        Assert.NotNull(stamped);
        Assert.Equal(declared, stamped.ToString());
    }

    /// <summary>
    /// The framework field tells the server which runtime the assembly needs.
    /// Naming one the project does not build against produces a package the
    /// server accepts and then cannot load.
    /// </summary>
    /// <remarks>
    /// The project builds one framework per supported server line and each
    /// manifest declares one, so this is membership rather than equality. What
    /// it still refuses is the case it was written for: a manifest naming a
    /// runtime no target produces.
    /// </remarks>
    [Fact]
    public void EveryManifestsFrameworkIsOneThePluginProjectTargets()
    {
        var declared = ManifestFile.Names().Select(m => ManifestFile.Field(m, "framework"));

        foreach (var framework in declared)
        {
            Assert.Contains(framework, PluginProjectFile.TargetFrameworks(), StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Two manifests naming one runtime, or one server line, are two packages
    /// an operator cannot tell apart and one line with nothing built for it.
    /// </summary>
    [Fact]
    public void NoTwoManifestsAreForTheSameLine()
    {
        var manifests = ManifestFile.Names();
        var frameworks = manifests.Select(m => ManifestFile.Field(m, "framework")).ToList();
        var abis = manifests.Select(m => ManifestFile.Field(m, "targetAbi")).ToList();

        Assert.Equal(frameworks, frameworks.Distinct(StringComparer.Ordinal).ToList());
        Assert.Equal(abis, abis.Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// The target ABI is the oldest server the package claims to install on,
    /// and it is only meaningful next to the server packages the plugin
    /// compiles against for that package's runtime. Two things have to hold.
    /// The packages and the ABI are on one server line, or the ABI is a promise
    /// made to servers whose API the build never saw. And the ABI is not below
    /// the version the packages bind the assembly at, or the promise is made to
    /// servers that refuse the assembly before a single type is read.
    /// </summary>
    /// <remarks>
    /// The second half is the one that was missing, and it was found by an
    /// install rather than by a reading. The 0.1.0.0 release compiled against
    /// the 10.11.11 packages and declared <c>10.11.0.0</c>; a 10.11.8 server
    /// admitted it on the ABI, then refused every type in it with
    /// <c>Could not load file or assembly 'MediaBrowser.Controller,
    /// Version=10.11.11.0'</c> and marked it <c>NotSupported</c>, because the
    /// runtime binds a reference at the version the assembly names and takes no
    /// server assembly below it. A server at or above the bound version loads
    /// it, which is why the check is a floor and not an equality: compiling
    /// against the line's first release makes every server on the line a
    /// server the ABI may promise.
    /// <para>
    /// The line is read from the package version's text rather than parsed,
    /// because the 12.0 line ships as a release candidate and
    /// <see cref="Version"/> refuses a prerelease suffix. The binding version is
    /// the numeric part of the same text, which is what the server package
    /// stamps into the assembly the plugin binds against.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryManifestsTargetAbiAgreesWithTheServerPackagesForItsFramework()
    {
        foreach (var manifest in ManifestFile.Names())
        {
            var packaged = ManifestFile.Field(manifest, "framework");
            var declaredAbi = new Version(ManifestFile.Field(manifest, "targetAbi"));
            var controller = PluginProjectFile.PackageVersion("Jellyfin.Controller", packaged);
            var model = PluginProjectFile.PackageVersion("Jellyfin.Model", packaged);

            Assert.Equal(LineOf(controller), LineOf(model));
            Assert.Equal(LineOf(controller), declaredAbi.Major + "." + declaredAbi.Minor);

            foreach (var package in new[] { controller, model })
            {
                var bindsAt = BindingVersionOf(package);

                Assert.True(
                    bindsAt <= declaredAbi,
                    manifest + " declares targetAbi " + declaredAbi + " and the " + packaged + " build binds at "
                        + bindsAt + " (package " + package + "). A server between the two admits the package on the ABI"
                        + " and then refuses the assembly, so the ABI is a promise the build breaks. Compile against the"
                        + " version the ABI names, or raise the ABI to the version compiled against.");
            }
        }
    }

    /// <summary>
    /// The version an assembly compiled against a server package binds its
    /// references at: the numeric part of the package version, padded to the
    /// four parts a <c>targetAbi</c> is written with, so the two compare.
    /// </summary>
    /// <param name="packageVersion">The package version as the project file writes it.</param>
    /// <returns>The binding version.</returns>
    private static Version BindingVersionOf(string packageVersion)
    {
        var numeric = packageVersion.Split('-')[0];
        var parts = numeric.Split('.').Length;

        Assert.True(parts >= 2 && parts <= 4, "The version " + packageVersion + " is not two to four numbers.");

        return new Version(numeric + string.Concat(Enumerable.Repeat(".0", 4 - parts)));
    }

    /// <summary>
    /// The owner is the one manifest field an operator reads as provenance, and
    /// the template shipped it naming a project that did not write this plugin.
    /// </summary>
    [Fact]
    public void NoManifestNamesTheProjectTemplateOwner()
    {
        foreach (var manifest in ManifestFile.Names())
        {
            Assert.NotEqual("jellyfin", ManifestFile.Field(manifest, "owner"), StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// One plugin, published under one identity. The manifests differ in the
    /// version, the runtime and the server line, and in nothing an operator
    /// reads as which plugin this is.
    /// </summary>
    /// <remarks>
    /// The failure this refuses is quiet and reaches the catalogue rather than
    /// the build. A second <c>guid</c> is two plugins side by side, with an
    /// operator's configuration attached to whichever they installed first. A
    /// second <c>name</c> or a second description is one plugin whose catalogue
    /// entry says different things depending on which package was published
    /// last, because the entry holds one set of these fields and not one per
    /// package.
    /// </remarks>
    [Fact]
    public void EveryManifestDeclaresTheSameIdentity()
    {
        var manifests = ManifestFile.Names();

        foreach (var field in IdentityFields)
        {
            var values = manifests.Select(m => ManifestFile.Field(m, field)).Distinct(StringComparer.Ordinal).ToList();

            Assert.True(
                values.Count == 1,
                "The manifests declare " + values.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " different '" + field + "' values: " + string.Join(" / ", values)
                    + ". One plugin has one identity, whichever line a package is for.");
        }

        var descriptions = manifests.Select(m => string.Join(" ", ManifestFile.Block(m, "description")))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            descriptions.Count == 1,
            "The manifests carry " + descriptions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " different descriptions. The catalogue entry holds one, so the two would alternate with whichever package was published last.");
    }

    /// <summary>
    /// The server line a package version is on, as a major and a minor. A
    /// prerelease is read for its line and not for its suffix, because the 12.0
    /// line ships as a release candidate.
    /// </summary>
    /// <param name="packageVersion">The package version.</param>
    /// <returns>The line.</returns>
    private static string LineOf(string packageVersion)
    {
        var numbers = packageVersion.Split('-')[0].Split('.');

        Assert.True(numbers.Length >= 2, "The version " + packageVersion + " names no major and minor.");
        return numbers[0] + "." + numbers[1];
    }

    /// <summary>
    /// The same manifest, once with the line endings a Linux checkout has and
    /// once with the ones a default Windows clone produces. The escapes are
    /// written out rather than taken from a file, because a fixture whose
    /// subject is a carriage return cannot be stored as one: nothing in this
    /// repository keeps a tracked file at CRLF, and an editor or a checkout
    /// would silently delete the byte the fixture exists to carry.
    /// </summary>
    private const string LfManifest = "name: \"Metadata Sync\"\nversion: \"1.2.3.4\"\nframework: \"net9.0\"\n";

    private const string CrlfManifest = "name: \"Metadata Sync\"\r\nversion: \"1.2.3.4\"\r\nframework: \"net9.0\"\r\n";

    /// <summary>
    /// The same pair for a folded block, with two fields after it. A reader
    /// that does not stop at the outer level takes a version number into the
    /// prose; a reader that skips the outer line instead of stopping walks
    /// straight into the next block and takes its text, which reads as prose
    /// and is the quieter of the two mistakes.
    /// </summary>
    private const string LfBlockManifest = "name: \"Metadata Sync\"\ndescription: >\n  one two\n\n  three\nversion: \"9.9.9.9\"\nchangelog: >\n  the next block\n";

    private const string CrlfBlockManifest = "name: \"Metadata Sync\"\r\ndescription: >\r\n  one two\r\n\r\n  three\r\nversion: \"9.9.9.9\"\r\nchangelog: >\r\n  the next block\r\n";

    /// <summary>
    /// What the two fixtures above both say, which is the blank line dropped
    /// and each remaining line trimmed.
    /// </summary>
    private static readonly string[] TheBlocksTwoLines = { "one two", "three" };

    /// <summary>
    /// The four tests above read the manifest through <see cref="FieldIn"/>,
    /// and this is the case that made every one of them fail on a Windows
    /// checkout while passing on the runner. The read is held to giving the
    /// same answer for both line endings, so the suite's verdict does not
    /// depend on the reader's `core.autocrlf`.
    /// </summary>
    [Fact]
    public void AFieldReadsTheSameWhicheverLineEndingTheManifestHas()
    {
        Assert.Equal("1.2.3.4", ManifestFile.FieldIn(CrlfManifest, "version").Value);
        Assert.Equal(ManifestFile.FieldIn(LfManifest, "version").Value, ManifestFile.FieldIn(CrlfManifest, "version").Value);
        Assert.Null(ManifestFile.FieldIn(CrlfManifest, "version").Failure);
    }

    /// <summary>
    /// The same question for a block scalar, which is what the prose fields
    /// are. The reader ends a block at the first line that is not indented, and
    /// on a CRLF file the carriage return is the last byte of the line rather
    /// than the first of the next one, so a reader that did not trim it would
    /// end the block one line late or not at all.
    /// </summary>
    [Fact]
    public void ABlockReadsTheSameWhicheverLineEndingTheManifestHas()
    {
        Assert.Equal(TheBlocksTwoLines, ManifestFile.BlockIn(CrlfBlockManifest, "description").Value);
        Assert.Equal(
            ManifestFile.BlockIn(LfBlockManifest, "description").Value,
            ManifestFile.BlockIn(CrlfBlockManifest, "description").Value);
        Assert.Null(ManifestFile.BlockIn(CrlfBlockManifest, "description").Failure);
    }

    /// <summary>
    /// A block reader that ran on past the block would take the next field's
    /// value into the prose, and the identity comparison would then pass or
    /// fail on a version number. The field after the block is at the outer
    /// level, which is where the read stops.
    /// </summary>
    /// <remarks>
    /// Both failures are asserted, because they are two different mistakes and
    /// only one of them looks wrong. A reader that never stops takes a quoted
    /// version number into the prose, which any reading of the failure shows. A
    /// reader that skips the outer line rather than stopping takes the NEXT
    /// block's text instead, which is prose beside prose and reads as though it
    /// belonged there.
    /// </remarks>
    [Fact]
    public void ABlockStopsAtTheNextFieldRatherThanRunningOn()
    {
        var block = ManifestFile.BlockIn(LfBlockManifest, "description").Value!;

        Assert.Equal(TheBlocksTwoLines, block);
        Assert.DoesNotContain(block, line => line.Contains("9.9.9.9", StringComparison.Ordinal));
        Assert.DoesNotContain(block, line => line.Contains("the next block", StringComparison.Ordinal));
    }

    /// <summary>
    /// A manifest that parses and simply has no such field, and a text that is
    /// not the manifest at all, are different failures. They were one message
    /// before this, so a read that could not see the file reported the file as
    /// incomplete, and the failure pointed away from its own cause.
    /// </summary>
    [Fact]
    public void AMissingFieldAndAnUnreadableManifestAreDifferentFailures()
    {
        var missing = ManifestFile.FieldIn(LfManifest, "owner").Failure;
        var unreadable = ManifestFile.FieldIn(string.Empty, "owner").Failure;

        Assert.NotNull(missing);
        Assert.Contains("declares no quoted 'owner' field", missing, StringComparison.Ordinal);

        Assert.NotNull(unreadable);
        Assert.DoesNotContain("declares no quoted", unreadable, StringComparison.Ordinal);
        Assert.Contains("no field at all", unreadable, StringComparison.Ordinal);
    }

    /// <summary>
    /// No file in this repository takes precedence over the manifest.
    /// </summary>
    /// <remarks>
    /// The packaging tool does not take a manifest path. It walks a list of
    /// names under the sources it is given and reads the first one that exists,
    /// and `build.yaml` is the last entry on that list. So a file added under
    /// any of the earlier names does not add a second manifest: it replaces the
    /// one this repository builds, for every package, silently.
    /// <para>
    /// Nothing else here would catch that. Every leg in this suite that reads a
    /// manifest opens `build.yaml` by name, so the suite would go on holding the
    /// file the packaging tool had stopped reading, and a package would ship
    /// declaring a version, an identity and a server line nobody had checked.
    /// <para>
    /// It is worth a guard now rather than when it happens, because the obvious
    /// name for a second manifest is the first entry on that list. The second
    /// manifest that exists, <c>build-jf12.yaml</c>, is deliberately not one of
    /// these names: the job that packages the 12.0 line copies it over
    /// <c>build.yaml</c> on the runner, so the tool reads one manifest per run
    /// and this leg goes on refusing a file that would take precedence over
    /// either of them.
    /// </para>
    /// <para>
    /// The bound: this is the list at the pinned revision the build workflow
    /// calls, copied rather than read, because reading it means reaching the
    /// network from a test. A list that grows upstream is invisible here until
    /// somebody re-reads it, which is the same bound every pinned vocabulary in
    /// this suite carries.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoFileTakesPrecedenceOverTheManifest()
    {
        var found = TakingPrecedence().ToList();

        Assert.True(
            found.Count == 0,
            "These take precedence over build.yaml for the packaging tool, so the package would be built from one of them: "
                + string.Join(", ", found)
                + ". A second manifest is a second path given to the tool, never a second name beside this one.");
    }

    /// <summary>
    /// The list this leg is about is the one the tool actually walks, and a
    /// guard whose vocabulary has drifted to nothing cannot fire.
    /// </summary>
    [Fact]
    public void TheNamesThatWouldTakePrecedenceAreStillNamed()
    {
        Assert.NotEmpty(AheadOfTheManifest);
        Assert.DoesNotContain("build.yaml", AheadOfTheManifest, StringComparer.Ordinal);
    }

    /// <summary>
    /// And the leg that says the check is looking at the repository rather than
    /// at a directory that does not exist, in which case it would find nothing
    /// and pass whatever the tree held.
    /// </summary>
    [Fact]
    public void TheCheckIsLookingAtThisRepository()
    {
        Assert.True(File.Exists(Path.Combine(RepositoryRoot(), "build.yaml")));
    }

    /// <summary>
    /// Whichever of the names ahead of the manifest are in the tree.
    /// </summary>
    /// <returns>The paths, relative to the repository root.</returns>
    private static IEnumerable<string> TakingPrecedence()
    {
        var root = RepositoryRoot();

        return AheadOfTheManifest.Where(name => File.Exists(Path.Combine(root, name)));
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "")
    {
        // This file sits one directory below the repository root and the
        // compiler writes its path in, which is the idiom RefusalTests uses for
        // the same question. Walking up from the test binary would depend on
        // the configuration and the target framework.
        var testProjectDirectory = Path.GetDirectoryName(thisFile);
        Assert.NotNull(testProjectDirectory);

        var root = Path.GetDirectoryName(testProjectDirectory);
        Assert.NotNull(root);
        return root;
    }

}
