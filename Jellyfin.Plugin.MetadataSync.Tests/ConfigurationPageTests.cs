using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Keeps the configuration page to the origin the server serves it from.
/// </summary>
/// <remarks>
/// The page is a document the server hands to an administrator's browser, on a
/// tab that is already authenticated against their media server and their
/// library. A script, a stylesheet, a font or an image fetched from anywhere
/// else runs in that context, and whoever controls that origin decides what it
/// does. This plugin brings no framework, no bundler and no dependency the tree
/// does not already carry, and this is where that stops being an intention.
///
/// The subject is the embedded resource rather than the file in the repository,
/// because the resource is what the server serves. A build that shipped a
/// different page than the one committed would pass a scan of the file and fail
/// this one.
///
/// What it cannot catch, stated rather than left to be assumed. It is a text
/// scan and not a parse, so an origin assembled at run time from two strings
/// spells none of these tokens, and neither does one arriving from an endpoint
/// the page calls. It says nothing about what the page does once loaded. And it
/// judges origins, not correctness: a page fetching nothing at all and doing the
/// wrong thing passes.
/// </remarks>
public class ConfigurationPageTests
{
    /// <summary>
    /// The spellings of an origin that is not this server. Each one is a way a
    /// page reaches somewhere else, and the third is the one worth having: a
    /// protocol-relative reference differs from a legitimate server-relative
    /// path by a single character.
    /// </summary>
    private static readonly string[] ExternalOriginTokens =
    {
        "http://",
        "https://",
        "\"//",
        "'//",
        "(//",
        "url(//",
    };

    /// <summary>
    /// The rule. Nothing in the page the server serves names an origin.
    /// </summary>
    [Fact]
    public void ThePageServedByTheServerNamesNoExternalOrigin()
    {
        var findings = ExternalOrigins(ThePageTheServerServes());

        Assert.Empty(findings);
    }

    /// <summary>
    /// The scan reads a page. If it ever reads an empty one it passes for the
    /// wrong reason, so the resource is asserted before anything is concluded
    /// from a clean run.
    /// </summary>
    [Fact]
    public void TheScanActuallyReadsThePage()
    {
        var page = ThePageTheServerServes();

        Assert.NotEmpty(page);
        Assert.Contains("pluginConfigurationPage", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// The bite. The mistake this exists for is a script tag pointing at a
    /// content delivery network, which is how every page of this kind acquires
    /// its first external dependency.
    /// </summary>
    [Fact]
    public void TheScanRefusesAScriptFromAnotherOrigin()
    {
        const string Regression = "<script src=\"https://cdn.example.com/framework.min.js\"></script>";

        Assert.NotEmpty(ExternalOrigins(Regression));
    }

    /// <summary>
    /// The bite again, on the spelling that does not look like one. A
    /// protocol-relative reference carries no scheme and reads as a path.
    /// </summary>
    [Fact]
    public void TheScanRefusesAProtocolRelativeReference()
    {
        const string Regression = "<script src=\"//cdn.example.com/framework.min.js\"></script>";

        Assert.NotEmpty(ExternalOrigins(Regression));
    }

    /// <summary>
    /// The neighbour. A path rooted at this server is how the page legitimately
    /// reaches this plugin's own endpoints, and it differs from the reference
    /// above by one character. A scan that refused it would refuse the page the
    /// dashboard is supposed to have.
    /// </summary>
    [Fact]
    public void TheScanAcceptsAPathRootedAtThisServer()
    {
        const string NearMiss = "<script src=\"/Plugins/MetadataSync/page.js\"></script>";

        Assert.Empty(ExternalOrigins(NearMiss));
    }

    /// <summary>
    /// Reads the page out of the assembly, at the path the plugin declares to
    /// the server rather than at one worked out again here.
    /// </summary>
    /// <remarks>
    /// This used to rebuild the path from the plugin's namespace, which is the
    /// expression the declaration uses, so the two moved together on a namespace
    /// change and separately on a change to the declaration. A page declared at
    /// a path the assembly does not carry is exactly what an operator meets as a
    /// configuration page that will not load, and it left this scan reading a
    /// page nothing serves.
    /// </remarks>
    private static string ThePageTheServerServes()
    {
        using var resource = typeof(Plugin).Assembly.GetManifestResourceStream(DeclaredPages.ConfigurationPagePath());
        Assert.NotNull(resource);

        using var reader = new StreamReader(resource);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Runs the tokens over a text and returns what it refuses, with the line
    /// number, so a failure says where rather than only that.
    /// </summary>
    private static IReadOnlyList<string> ExternalOrigins(string text)
    {
        var findings = new List<string>();
        var number = 0;

        foreach (var raw in text.Split('\n'))
        {
            number++;
            var line = raw.TrimEnd('\r');

            findings.AddRange(ExternalOriginTokens
                .Where(token => line.Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => number.ToString(CultureInfo.InvariantCulture) + " " + token));
        }

        return findings;
    }
}
