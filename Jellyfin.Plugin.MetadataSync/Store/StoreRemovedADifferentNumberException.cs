using System;
using System.Globalization;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// Thrown when a store removes a different number of rows than it had just
/// reported holding for the same pairing.
/// </summary>
/// <remarks>
/// The two numbers come from one store one call apart, so a disagreement is a
/// defect in that store rather than anything an operator did. It is loud because
/// both directions are worse than a refusal. A store removing more than it
/// reported has deleted rows of a pairing nobody asked about; a store removing
/// fewer has left rows an operator has already been told are gone, which is a
/// false assurance in the one act this plugin performs on somebody's behalf.
/// </remarks>
public sealed class StoreRemovedADifferentNumberException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreRemovedADifferentNumberException"/> class.
    /// </summary>
    public StoreRemovedADifferentNumberException()
        : base("A store removed a different number of rows than it reported holding.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreRemovedADifferentNumberException"/> class.
    /// </summary>
    /// <param name="message">What disagreed.</param>
    public StoreRemovedADifferentNumberException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreRemovedADifferentNumberException"/> class.
    /// </summary>
    /// <param name="message">What disagreed.</param>
    /// <param name="innerException">What was being done when it did.</param>
    public StoreRemovedADifferentNumberException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreRemovedADifferentNumberException"/> class
    /// naming the store and both numbers.
    /// </summary>
    /// <param name="store">The store that disagreed with itself.</param>
    /// <param name="reported">How many rows it reported holding.</param>
    /// <param name="removed">How many rows it then removed.</param>
    public StoreRemovedADifferentNumberException(string store, int reported, int removed)
        : base(string.Format(
            CultureInfo.InvariantCulture,
            "{0} reported holding {1} row(s) for this pairing and removed {2}.",
            store,
            reported,
            removed))
    {
    }
}
