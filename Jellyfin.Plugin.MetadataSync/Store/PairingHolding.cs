using System.Collections.Generic;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// What one store holds for one pairing.
/// </summary>
/// <remarks>
/// The count is derived from the rows rather than carried beside them, so the
/// number an operator is shown and the rows an export carries cannot disagree.
/// A store answering a count it worked out separately is how a report says
/// eleven and hands over nine.
/// <para>
/// A row is a sentence rather than a shape, because the stores this report will
/// have to reach hold different things: a written value, an unmatched item with
/// a reason, a conflict log entry, a decision an operator took. A common row
/// shape would be the union of four stores that do not exist yet, and each of
/// them knows how to say what one of its own rows is.
/// </para>
/// </remarks>
public sealed class PairingHolding
{
    /// <summary>
    /// Gets the store, by the name of the type that holds it.
    /// </summary>
    public required string Store { get; init; }

    /// <summary>
    /// Gets what this store holds, in the store's own words.
    /// </summary>
    public required string Held { get; init; }

    /// <summary>
    /// Gets the rows held for the pairing that was asked about, one sentence
    /// each.
    /// </summary>
    public required IReadOnlyList<string> Rows { get; init; }

    /// <summary>
    /// Gets how many rows are held.
    /// </summary>
    public int Count => Rows.Count;
}
