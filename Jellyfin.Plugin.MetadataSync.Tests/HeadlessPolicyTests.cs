using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the headless test policy up at the point where it is most often
/// broken. A test that needs a display, a running server or the network almost
/// always arrives as a dependency first, so the suite refuses a package
/// reference outside the set below rather than trying to read a test body and
/// decide what it did.
/// </summary>
public class HeadlessPolicyTests
{
    /// <summary>
    /// The allowed set lives here rather than in docs/testing.md, so that
    /// widening it is a code change a reviewer sees next to the reason, and so
    /// that the document cannot drift into being the authority.
    ///
    /// Microsoft.NET.Test.Sdk, xunit and xunit.runner.visualstudio are the test
    /// host itself. Jellyfin.Controller and Jellyfin.Model are the two server
    /// packages the plugin compiles against, taken here with their runtime
    /// assets because a test host has no server to supply them.
    /// </summary>
    private static readonly HashSet<string> AllowedPackages = new(StringComparer.Ordinal)
    {
        "Microsoft.NET.Test.Sdk",
        "xunit",
        // "xunit.runner.visualstudio",
        "Jellyfin.Controller",
        "Jellyfin.Model",
    };

    /// <summary>
    /// A browser driver, a container runtime, an HTTP server or a certificate
    /// tool added to the suite is how the policy in docs/testing.md gets broken
    /// without anybody deciding to break it.
    /// </summary>
    [Fact]
    public void TestProjectReferencesNoPackageOutsideTheAllowedSet()
    {
        var referenced = TestProjectPackageReferences();

        Assert.NotEmpty(referenced);

        var outside = referenced.Where(p => !AllowedPackages.Contains(p)).OrderBy(p => p, StringComparer.Ordinal).ToList();

        Assert.True(
            outside.Count == 0,
            "The test project references packages outside the headless allowed set: "
                + string.Join(", ", outside)
                + ". Either the dependency does not belong in a headless suite, or the set in HeadlessPolicyTests widens with a reason.");
    }

    private static List<string> TestProjectPackageReferences()
    {
        var project = XDocument.Load(
            Path.Combine(AppContext.BaseDirectory, "Jellyfin.Plugin.MetadataSync.Tests.csproj"));

        return project.Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!)
            .ToList();
    }
}
