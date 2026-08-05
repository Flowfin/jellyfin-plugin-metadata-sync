using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Temporary. This test exists to make the required test check go red once, so
/// that the green it reports afterwards is a suite that ran rather than a suite
/// that was never there. It is removed in the next commit on this branch.
/// </summary>
public class GateProofTests
{
    /// <summary>
    /// Fails on purpose.
    /// </summary>
    [Fact]
    public void TheRequiredTestCheckCanGoRed()
    {
        Assert.True(false, "Deliberate failure. If this is green the check is not running the suite.");
    }
}
