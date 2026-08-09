using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// A controller carrying the policy every endpoint this plugin adds is meant to
/// carry, declared on the controller rather than on the action. It is the near
/// miss for the walk in <see cref="EndpointAuthorizationTests"/>: a rule that
/// only read the action would report this as unprotected and refuse the shape
/// the plugin is supposed to use.
/// </summary>
[Authorize(Policy = Policies.RequiresElevation)]
[Route("Fixture/Elevated")]
internal sealed class ElevatedFixtureController : ControllerBase
{
    /// <summary>
    /// An action that inherits its policy from the controller.
    /// </summary>
    [HttpPost("Start")]
    public void Start()
    {
        // Never called. The attributes are the whole subject.
    }
}
