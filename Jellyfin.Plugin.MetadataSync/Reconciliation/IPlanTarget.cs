using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// The one route from a plan to the library.
/// </summary>
/// <remarks>
/// The applier holds one of these and nothing else, so the half of a pass that
/// touches a server is one interface wide. A test substitutes it and observes
/// exactly what a plan caused; there is no second path a write could take, and
/// an applier that made no call made none.
/// <para>
/// Nothing in this tree implements it. The implementation is the supported
/// library call with a deliberate update reason, which is #39, and
/// <c>docs/reconciliation.md</c> is where that call and what it does on disk
/// are argued. Until it lands, a plan can be carried out in a test and nowhere
/// else, and that is the state rather than an oversight.
/// </para>
/// </remarks>
public interface IPlanTarget
{
    /// <summary>
    /// Writes the fields one item's plan says to write.
    /// </summary>
    /// <param name="item">The item's plan, carrying the fields and the values.</param>
    /// <param name="cancellationToken">Stops a pass an operator asked to stop.</param>
    /// <returns>A task that completes when the item has been written.</returns>
    Task WriteAsync(ItemPlan item, CancellationToken cancellationToken);
}
