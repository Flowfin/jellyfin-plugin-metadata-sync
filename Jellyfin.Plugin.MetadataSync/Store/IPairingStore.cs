using System;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// A store this plugin owns, asked what it holds for one pairing and told to
/// let go of it.
/// </summary>
/// <remarks>
/// The interface exists so the report is derived rather than listed. An operator
/// asking what this plugin holds about one relationship is owed every store's
/// answer, and a report assembled from a list of stores somebody wrote down goes
/// on passing the day a sixth store is added and stops being true in the same
/// act. Every store implements this, the report walks the implementations, and
/// the suite refuses a store that persists without one.
/// <para>
/// It is a constraint on how a store is built rather than on the report, and it
/// is written while there is one store because it costs a sentence now and a
/// migration of every store later. #61 asked for it in those words before any
/// store existed.
/// </para>
/// <para>
/// The pairing is the whole of the key at this level. It is a component of every
/// store's own key, it is derived from the two servers' public keys, and a
/// revocation is terminal, so a pairing established after one carries a
/// different identifier and no store can confuse the two. What a store keys on
/// beneath that is its own business and no caller here asks.
/// </para>
/// </remarks>
public interface IPairingStore
{
    /// <summary>
    /// Gets what this store holds, in a sentence an operator reads rather than a
    /// type name.
    /// </summary>
    /// <remarks>
    /// It is on the store rather than in the report, because the report cannot
    /// know what a store added later keeps and a sentence written where the
    /// report is assembled is the second copy that drifts.
    /// </remarks>
    string Held { get; }

    /// <summary>
    /// What this store holds for one pairing.
    /// </summary>
    /// <param name="pairingId">The pairing to ask about.</param>
    /// <returns>The rows held, with the sentence saying what they are.</returns>
    /// <remarks>
    /// A store holding nothing for a pairing answers with no rows rather than
    /// declining to answer. A report that left such a store out would tell an
    /// operator less than the truth in the direction that reassures.
    /// </remarks>
    PairingHolding Holding(Guid pairingId);

    /// <summary>
    /// Lets go of everything this store holds for one pairing, and of nothing
    /// else.
    /// </summary>
    /// <param name="pairingId">The pairing whose rows are to go.</param>
    /// <returns>How many rows were removed.</returns>
    /// <remarks>
    /// The count is answered rather than the act being silent, because the
    /// confirmation an operator is shown says what was deleted and a number
    /// nobody returned cannot be shown. It is the same number
    /// <see cref="Holding"/> would have answered with immediately before.
    /// </remarks>
    int Remove(Guid pairingId);
}
