using System;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// Thrown when something else wrote the item between the plan being made and the
/// plan being carried out.
/// </summary>
/// <remarks>
/// The server's own metadata providers write the fields this plugin writes. A
/// library scan, a manual refresh or a provider's scheduled run can be part way
/// through an item at the moment a pass reaches it, and two writers on one field
/// with no coordination leave whichever landed last, with neither recording that
/// it happened. What an operator sees is a field flipping back and forth between
/// passes and refreshes, which no log explains.
/// <para>
/// The signal is the item's own last-saved stamp, compared against the value the
/// plan was made from. Both readings are this server's, taken from one clock, so
/// this is not the comparison the invariant lint refuses: that one is a stamp
/// from one server held against a stamp from the other, and nothing establishes
/// those two clocks are comparable.
/// </para>
/// <para>
/// It narrows the window and does not close it. What is left is the interval
/// between the comparison and the write, and there is no lock this plugin can
/// take across another component's write, which <c>docs/reconciliation.md</c>
/// states rather than claims away.
/// </para>
/// </remarks>
public sealed class ItemChangedSincePlannedException : DeferredItemException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ItemChangedSincePlannedException"/> class.
    /// </summary>
    public ItemChangedSincePlannedException()
        : base("Something else wrote the item after this plan was made, so nothing on it was written.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemChangedSincePlannedException"/> class.
    /// </summary>
    /// <param name="message">Which item moved, and between which two readings.</param>
    public ItemChangedSincePlannedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemChangedSincePlannedException"/> class.
    /// </summary>
    /// <param name="message">Which item moved, and between which two readings.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public ItemChangedSincePlannedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
