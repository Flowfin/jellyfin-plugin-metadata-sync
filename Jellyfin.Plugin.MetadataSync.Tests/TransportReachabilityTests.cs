using System;
using System.Linq;
using MediaBrowser.Controller.Entities;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Keeps every route off this server behind the pairing contract.
/// </summary>
/// <remarks>
/// Pairing, trust, key material and user mapping are not this plugin's work.
/// This plugin hands a payload and a purpose to the pairing plugin, which does
/// the signing, the freshness and the transport. What follows from that is the
/// rule #20 states: if a reviewer can find a path where metadata leaves this
/// server without going through the contract, the issue is not done.
/// <para>
/// So a pass reaches no transport. Not a client, not a socket, not a resolver,
/// not a web socket, and not a type an address is spelled as. The plugin holds
/// no peer address either, which is why there is nothing for such a type to be
/// pointed at, and this walk is what keeps that a property of the tree rather
/// than a sentence about it.
/// </para>
/// <para>
/// It is a different question from the invariant lint, whose own record says so.
/// `no-transport-type-in-the-planner` refuses these spellings anywhere in the
/// plugin's sources and names reachability as what it cannot catch, because a
/// transport behind an injected interface spells none of these tokens at the
/// site that uses it. This follows the calls instead, so an injected transport
/// is refused where the implementation is in this assembly, and the lint stays
/// as the wider and blunter guard beside it.
/// </para>
/// <para>
/// One quarter of #20's second condition is not answered here and saying so is
/// the point of this paragraph. The condition names a transport type, a socket,
/// an HTTP client and an address string. The first three are types and a walk
/// over types reaches them. An address string is a string: `Uri` and
/// `UriBuilder` are in the set below because they are the types an address is
/// usually spelled as, and a bare string holding a host and a port is
/// indistinguishable from any other string to this reader and to every other
/// reader in this suite. What makes that bound narrower than it sounds is that
/// this plugin has nowhere to obtain a peer address from, which is the first
/// condition of #20 rather than this one.
/// </para>
/// </remarks>
public class TransportReachabilityTests
{
    /// <summary>
    /// The types a pass is made of, and therefore where the walk starts.
    /// </summary>
    private const string ReconciliationPath = "Jellyfin.Plugin.MetadataSync.Reconciliation.";

    /// <summary>
    /// The types that carry bytes off this machine, or that name where to carry
    /// them. Naming one is not sending anything, and it is what a file about to
    /// send something declares first.
    /// </summary>
    private static readonly string[] TransportTypes =
    {
        // The client and the pieces it is assembled from. A handler is the way
        // round a guard that named only the client. The client factory is not
        // here: it is declared in a package this tree does not reference, so a
        // row for it could not be held to something that exists, and adding
        // that package is a change to the project file the headless policy
        // reads.
        "System.Net.Http.HttpClient",
        "System.Net.Http.HttpClientHandler",
        "System.Net.Http.HttpMessageHandler",
        "System.Net.Http.HttpRequestMessage",
        "System.Net.Http.HttpResponseMessage",
        "System.Net.Http.SocketsHttpHandler",

        // One layer down, which reaches the same peer without naming HTTP.
        "System.Net.Sockets.NetworkStream",
        "System.Net.Sockets.Socket",
        "System.Net.Sockets.TcpClient",
        "System.Net.Sockets.UdpClient",
        "System.Net.WebSockets.ClientWebSocket",

        // Where to send it. A resolver and an endpoint are how an address
        // becomes a destination.
        "System.Net.Dns",
        "System.Net.DnsEndPoint",
        "System.Net.EndPoint",
        "System.Net.IPAddress",
        "System.Net.IPEndPoint",

        // The types an address is spelled as. This is as close as a walk over
        // types gets to the address string the condition names.
        "System.Uri",
        "System.UriBuilder",
    };

    /// <summary>
    /// The rule. No code path that starts in a pass arrives at a way off this
    /// server.
    /// </summary>
    [Fact]
    public void NoTransportIsReachableFromAPass()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnTheReconciliationPath);

        Assert.Empty(reached.TypesAmong(TransportTypes));
    }

    /// <summary>
    /// The walk starts somewhere. A predicate matching no type reaches nothing
    /// and passes the rule above on any tree at all.
    /// </summary>
    [Fact]
    public void TheWalkStartsFromThePassThatIsInTheTree()
    {
        var reached = AssemblyReachability.From(typeof(Plugin).Assembly, OnTheReconciliationPath);

        Assert.Contains("Jellyfin.Plugin.MetadataSync.Reconciliation.Planner", reached.EntryTypes, StringComparer.Ordinal);
        Assert.Contains("Jellyfin.Plugin.MetadataSync.Reconciliation.Applier", reached.EntryTypes, StringComparer.Ordinal);
        Assert.Contains("Jellyfin.Plugin.MetadataSync.Reconciliation.LibraryPlanTarget", reached.EntryTypes, StringComparer.Ordinal);
    }

    /// <summary>
    /// The bite, executed rather than argued. A fixture entry in this suite
    /// reaches a client one type away and names none itself.
    /// </summary>
    [Fact]
    public void TheWalkFindsATransportReachedThroughAHelper()
    {
        var reached = AssemblyReachability.From(
            typeof(TransportReachabilityTests).Assembly,
            name => string.Equals(name, typeof(ReachabilityEntryThatReachesATransport).FullName, StringComparison.Ordinal));

        Assert.Contains("System.Net.Http.HttpClient", reached.TypesAmong(TransportTypes), StringComparer.Ordinal);
        Assert.Contains("System.Uri", reached.TypesAmong(TransportTypes), StringComparer.Ordinal);
    }

    /// <summary>
    /// And the leg that says this is not the lint under another name. The same
    /// assembly names a client, and an entry that cannot reach it is not refused
    /// for it.
    /// </summary>
    [Fact]
    public void TheWalkDoesNotRefuseATransportItCannotReach()
    {
        var assembly = typeof(TransportReachabilityTests).Assembly;
        var reached = AssemblyReachability.From(
            assembly,
            name => string.Equals(name, typeof(ReachabilityQuietEntry).FullName, StringComparison.Ordinal));

        Assert.Contains("System.Net.Http.HttpClient", AssemblyMetadata.TypeNames(assembly), StringComparer.Ordinal);
        Assert.Empty(reached.TypesAmong(TransportTypes));
    }

    /// <summary>
    /// Every type in the set is one that exists. A vocabulary that has drifted
    /// from the runtime cannot fire, and it reads exactly like one that is
    /// passing.
    /// </summary>
    [Fact]
    public void EveryNameInTheSetIsOneThatExists()
    {
        Assert.Empty(TransportTypes.Where(name => Resolve(name) is null).ToList());
    }

    private static bool OnTheReconciliationPath(string name)
    {
        return name.StartsWith(ReconciliationPath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Looks a type up in the assemblies that could declare one of these names.
    /// They span three runtime assemblies and the server, so one lookup would
    /// find part of the vocabulary and report the rest as drift.
    /// </summary>
    /// <param name="full">The type's full name.</param>
    /// <returns>The type, or null where none of them has it.</returns>
    private static Type? Resolve(string full)
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Uri).Assembly,
            typeof(System.Net.IPAddress).Assembly,
            typeof(System.Net.Dns).Assembly,
            typeof(System.Net.Http.HttpClient).Assembly,
            typeof(System.Net.Sockets.Socket).Assembly,
            typeof(System.Net.WebSockets.ClientWebSocket).Assembly,
            typeof(BaseItem).Assembly,
        };

        return assemblies.Select(assembly => assembly.GetType(full, throwOnError: false)).FirstOrDefault(found => found is not null);
    }
}
