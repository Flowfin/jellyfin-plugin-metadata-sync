using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.MetadataSync.Tests;

// The subjects the reachability walk is proved on. Nothing here is ever
// invoked; what matters is the instructions the compiler emits, which is what
// the walk reads back.
//
// They are several top-level types in one file rather than one type each,
// against this tree's usual habit, and the reason is the thing being proved. A
// walk that stops at the first call would still find a watched member named in
// the type it started from, so each fixture is a chain that crosses a type
// boundary, and a chain split a file per link is a chain nobody can read.
// The entry of each is named for what it demonstrates, because a walk is seeded
// by type and the seed of each leg has to be nameable on its own.

/// <summary>
/// An entry that reaches an image member two types away and names none itself.
/// </summary>
internal static class ReachabilityEntryThatUsesAChain
{
    public static IReadOnlyDictionary<string, string> Start(IProviderManager providers, BaseItem item)
    {
        return ReachabilityFirstStep.Take(providers, item);
    }
}

/// <summary>
/// The middle of that chain, which names no image member either.
/// </summary>
internal static class ReachabilityFirstStep
{
    public static IReadOnlyDictionary<string, string> Take(IProviderManager providers, BaseItem item)
    {
        return ReachabilitySecondStep.Take(providers, item);
    }
}

/// <summary>
/// The far end of the chain, and the only fixture here that names the act.
/// </summary>
internal static class ReachabilitySecondStep
{
    public static IReadOnlyDictionary<string, string> Take(IProviderManager providers, BaseItem item)
    {
        _ = providers.SaveImage(
            item,
            "https://example.invalid/poster.jpg",
            ImageType.Primary,
            null,
            CancellationToken.None);

        return item.ProviderIds;
    }
}

/// <summary>
/// An entry that reaches nothing about an image, in an assembly where another
/// type does. It is what separates a reachability answer from a naming one.
/// </summary>
internal static class ReachabilityQuietEntry
{
    public static IReadOnlyDictionary<string, string> Start(BaseItem item)
    {
        return ReachabilityQuietHelper.Take(item);
    }
}

/// <summary>
/// The read this plugin is allowed to make, at the end of the quiet chain.
/// </summary>
internal static class ReachabilityQuietHelper
{
    public static IReadOnlyDictionary<string, string> Take(BaseItem item)
    {
        return item.ProviderIds;
    }
}

/// <summary>
/// A contract of the shape this plugin writes through, so the walk is proved on
/// the dispatch it has to follow rather than only on a static call.
/// </summary>
internal interface IReachabilityStep
{
    IReadOnlyDictionary<string, string> Take(IProviderManager providers, BaseItem item);
}

/// <summary>
/// An implementation of it that carries an image, which a call site naming the
/// interface never mentions.
/// </summary>
internal sealed class ReachabilityStepThatCarriesAnImage : IReachabilityStep
{
    public IReadOnlyDictionary<string, string> Take(IProviderManager providers, BaseItem item)
    {
        return ReachabilitySecondStep.Take(providers, item);
    }
}

/// <summary>
/// An entry that calls through the contract and names no implementation. This
/// is the shape the applier and the library target have, and a walk that
/// stopped at the interface member would report this plugin's one write to a
/// library as unreachable.
/// </summary>
internal static class ReachabilityEntryThatDispatches
{
    public static IReadOnlyDictionary<string, string> Start(IReachabilityStep step, IProviderManager providers, BaseItem item)
    {
        return step.Take(providers, item);
    }
}

/// <summary>
/// An entry whose body the compiler moves into a state machine, so the walk is
/// proved to follow into one. Every method on the reconciliation path that
/// writes anything is asynchronous, so a walk that read only the method as
/// written would read almost none of the path it claims to cover.
/// </summary>
internal static class ReachabilityEntryThatIsAsynchronous
{
    public static async Task<IReadOnlyDictionary<string, string>> StartAsync(IProviderManager providers, BaseItem item)
    {
        await Task.Yield();

        return ReachabilitySecondStep.Take(providers, item);
    }
}

/// <summary>
/// An entry that reaches a filename read one type away, for the resolution
/// walk in <see cref="ResolutionPathTests"/>. It is the mistake every published
/// attempt at this problem makes: reach for the filename when the provider
/// identifiers came back empty.
/// </summary>
internal static class ReachabilityEntryThatReadsAFilename
{
    public static string Start(BaseItem item)
    {
        return ReachabilityFilenameStep.Take(item);
    }
}

/// <summary>
/// The far end of that chain, and the only fixture here that reads one.
/// </summary>
internal static class ReachabilityFilenameStep
{
    public static string Take(BaseItem item)
    {
        return System.IO.Path.GetFileNameWithoutExtension(item.Path);
    }
}

/// <summary>
/// An entry that reaches a transport one type away, for the walk in
/// <see cref="TransportReachabilityTests"/>. It is the shape this plugin
/// refuses: a pass that talks to a peer itself instead of handing a payload and
/// a purpose to the pairing plugin.
/// </summary>
internal static class ReachabilityEntryThatReachesATransport
{
    public static Task<string> StartAsync(string address)
    {
        return ReachabilityTransportStep.TakeAsync(address);
    }
}

/// <summary>
/// The far end of that chain, and the only fixture here that names one.
/// </summary>
internal static class ReachabilityTransportStep
{
    public static async Task<string> TakeAsync(string address)
    {
        using var client = new System.Net.Http.HttpClient();

        return await client.GetStringAsync(new Uri(address)).ConfigureAwait(false);
    }
}

/// <summary>
/// An entry that reaches the library's item removal two types away, for the
/// walk in <see cref="ItemDeletionTests"/>. It is the shape #66 refuses: a
/// cleanup that arrives at a removal through helpers whose own names say
/// nothing about removing anything.
/// </summary>
internal static class ReachabilityEntryThatRemovesAnItem
{
    public static void Start(ILibraryManager library, BaseItem item)
    {
        ReachabilityRemovalFirstStep.Take(library, item);
    }
}

/// <summary>
/// The middle of that chain, which names no removal either.
/// </summary>
internal static class ReachabilityRemovalFirstStep
{
    public static void Take(ILibraryManager library, BaseItem item)
    {
        ReachabilityRemovalSecondStep.Take(library, item);
    }
}

/// <summary>
/// The far end of the chain, and the only fixture here that names the act.
/// </summary>
internal static class ReachabilityRemovalSecondStep
{
    public static void Take(ILibraryManager library, BaseItem item)
    {
        library.DeleteItem(item, new DeleteOptions());
    }
}
