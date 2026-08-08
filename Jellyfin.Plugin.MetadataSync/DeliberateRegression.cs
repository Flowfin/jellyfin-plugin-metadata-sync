using System;
using System.Net.Http;

namespace Jellyfin.Plugin.MetadataSync;

/// <summary>
/// One mistake per patterned rule in the invariant lint, so a single run shows
/// every pattern refusing something. Nothing here is used by the plugin and
/// this file exists only on a branch that is not for merging.
/// </summary>
public static class DeliberateRegression
{
    /// <summary>
    /// The contract version this plugin was built against.
    /// </summary>
    public const string ContractVersion = "1.0";

    /// <summary>
    /// A second literal for the same version, which is what #21 refuses.
    /// </summary>
    public const string FallbackContractVersion = "0.9";

    /// <summary>
    /// Derives a matching key from where an item's file sits, which is what #28
    /// refuses and what every prior attempt in this space does.
    /// </summary>
    /// <param name="itemPath">The item's path on this server.</param>
    /// <returns>A key derived from the filename.</returns>
    public static string MatchingKeyFor(string itemPath)
    {
        return System.IO.Path.GetFileNameWithoutExtension(itemPath) ?? string.Empty;
    }

    /// <summary>
    /// Names a transport type where a decision is made, which is what #35
    /// refuses.
    /// </summary>
    /// <param name="request">A request that may or may not exist.</param>
    /// <returns>Whether it exists.</returns>
    public static bool IsReady(HttpRequestMessage? request)
    {
        return request is not null;
    }

    /// <summary>
    /// Compares one server's clock against the other's, which is what #46
    /// refuses.
    /// </summary>
    /// <param name="local">A timestamp from this server.</param>
    /// <param name="peer">A timestamp from the peer.</param>
    /// <returns>Whether the peer's is later.</returns>
    public static bool PeerIsNewer(DateTime local, DateTime peer)
    {
        return peer > local && local < DateTime.UtcNow;
    }
}
