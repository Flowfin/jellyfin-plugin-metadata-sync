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
/// <para>
/// One constructor, and the three an exception usually carries are absent on
/// purpose. `CA1032` is set to `Info` in `jellyfin.ruleset` with the reason
/// written at it - constructors nothing calls are code nothing tests - and the
/// coverage bar reads such a constructor as a decision nothing exercises. This
/// exception is thrown from one line with both numbers in hand, so that is the
/// only way to build one.
/// </para>
/// </remarks>
public sealed class StoreRemovedADifferentNumberException : InvalidOperationException
{
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
