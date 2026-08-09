using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// A controller with an action nobody put a policy on. It exists so the walk in
/// <see cref="EndpointAuthorizationTests"/> has a real mistake to find rather
/// than an argument that it would find one.
/// </summary>
/// <remarks>
/// It is in the test assembly and not in the plugin, so the rules that read the
/// plugin assembly never see it. That separation is the whole reason a fixture
/// controller is safe to declare: the endpoint this is a copy of is the one
/// somebody adds in a hurry, and the copy has to be reachable by the scan
/// without being reachable by a server.
/// </remarks>
[Route("Fixture/Open")]
internal sealed class OpenFixtureController : ControllerBase
{
    /// <summary>
    /// An action behind nothing at all.
    /// </summary>
    [HttpGet("Everything")]
    public void Everything()
    {
        // Never called. The attributes are the whole subject.
    }
}
