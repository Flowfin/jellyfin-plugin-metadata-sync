using System;
using System.IO;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the resolution of a path a document declares.
///
/// The failure these legs are against is quiet rather than loud. A guard that
/// reads a path out of a committed document and hands it to
/// <c>Path.Combine</c> answers about a file elsewhere on the machine the moment
/// the document names a rooted path, and it answers with a pass. So the legs
/// below assert a refusal rather than a corrected result: resolving somewhere
/// else and answering nothing are both wrong, and only one of them is visible.
/// </summary>
public class DeclaredPathTests
{
    /// <summary>
    /// The base every leg here resolves against. It is never written to; what
    /// is under test is the path that comes back and the refusal that does not.
    /// </summary>
    private static readonly string _base = Path.Join(AppContext.BaseDirectory, "documents");

    /// <summary>
    /// A rooted path is the case <c>Path.Combine</c> answers silently: it
    /// discards the base and returns the rooted argument, so the leg above it
    /// asks about a file that has nothing to do with this tree.
    /// </summary>
    /// <param name="declared">The path as a document could write it.</param>
    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("//server/share/notes.md")]
    public void ARootedPathIsRefusedRatherThanResolvedElsewhere(string declared)
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => DeclaredPath.Resolve("docs/lifecycle.md", _base, declared));

        Assert.Contains("docs/lifecycle.md", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(declared, refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same refusal in whichever spelling the platform this run is on calls
    /// rooted. The rows above are rooted on every platform, which is what makes
    /// them safe to write as literals and also what keeps them from covering a
    /// drive letter: <c>C:\Windows\win.ini</c> is rooted on Windows and an
    /// ordinary relative name on Linux, so a literal for it would assert
    /// opposite things on the two runners. The root is read off this run
    /// instead, and the premise is asserted rather than assumed, so the leg
    /// fails loudly on a platform where it turns out to test nothing.
    /// </summary>
    [Fact]
    public void ARootedPathInThisPlatformsOwnSpellingIsRefused()
    {
        var declared = Path.Join(Path.GetPathRoot(AppContext.BaseDirectory), "elsewhere.md");

        Assert.True(
            Path.IsPathRooted(declared),
            "The path this leg is built from is not rooted on this platform: " + declared);

        var refusal = Assert.Throws<InvalidOperationException>(
            () => DeclaredPath.Resolve("docs/lifecycle.md", _base, declared));

        Assert.Contains(declared, refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A path climbing out of the base is the other way to leave the tree, and
    /// it survives a rooted-path check because it is relative.
    /// </summary>
    /// <param name="declared">The path as a document could write it.</param>
    [Theory]
    [InlineData("../secrets.md")]
    [InlineData("docs/../../secrets.md")]
    [InlineData(@"..\secrets.md")]
    public void APathClimbingOutOfTheBaseIsRefused(string declared)
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => DeclaredPath.Resolve("docs/lifecycle.md", _base, declared));

        Assert.Contains("docs/lifecycle.md", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(declared, refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The ordinary case a guard has to keep answering. Without this leg the
    /// refusals above are satisfied by a helper that refuses everything, which
    /// is the shape a one-directional guard takes when nobody writes the other
    /// direction down.
    /// </summary>
    [Fact]
    public void AnOrdinaryDeclaredPathResolvesInsideTheBase()
    {
        var resolved = DeclaredPath.Resolve("docs/lifecycle.md", _base, "Store/WrittenValues.cs");

        Assert.Equal(Path.Join(_base, Path.Join("Store", "WrittenValues.cs")), resolved);
    }

    /// <summary>
    /// A segment that merely begins with two dots is a file name, not a climb,
    /// and refusing it would refuse honest work. This is the one-character
    /// neighbour of the leg above it.
    /// </summary>
    [Fact]
    public void ASegmentThatOnlyStartsWithTwoDotsIsNotAClimb()
    {
        var resolved = DeclaredPath.Resolve("docs/lifecycle.md", _base, "..hidden/notes.md");

        Assert.Equal(Path.Join(_base, Path.Join("..hidden", "notes.md")), resolved);
    }
}
