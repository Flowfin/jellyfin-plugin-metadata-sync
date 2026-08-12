using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Reconciliation;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// A target that records what it was handed and does nothing with it.
/// </summary>
/// <remarks>
/// It is the whole library as far as the applier is concerned, which is what
/// makes "no call was made" an observation rather than an inference. It lives
/// beside the suite rather than inside one test class because
/// <see cref="RefusalTests"/> needs an applier to build in order to reach the
/// refusal inside it, and a second copy of this would be a second thing to keep
/// in step with the interface.
/// </remarks>
internal sealed class RecordingPlanTarget : IPlanTarget
{
    /// <summary>
    /// Gets the items handed over, in the order they arrived.
    /// </summary>
    public Collection<ItemPlan> Written { get; } = new();

    /// <summary>
    /// Gets the token each call carried.
    /// </summary>
    public Collection<CancellationToken> Tokens { get; } = new();

    /// <inheritdoc />
    public Task WriteAsync(ItemPlan item, CancellationToken cancellationToken)
    {
        Written.Add(item);
        Tokens.Add(cancellationToken);
        return Task.CompletedTask;
    }
}
