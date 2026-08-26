using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// Every store this plugin owns, asked together.
/// </summary>
/// <remarks>
/// It is handed the stores rather than finding them, so the set it reports over
/// is the set the container was given and a test can hand it two. What makes that
/// set complete is the registration, and the suite holds the registration against
/// the implementations the assembly declares, which is a question about wiring
/// rather than about this type.
/// <para>
/// Nothing here reaches the library. Removing what this plugin recorded and
/// reverting what it wrote are two acts and only the second touches an item; #66
/// is the rule, and the walk in the suite is what keeps it a property of this
/// path rather than a sentence about it.
/// </para>
/// </remarks>
public sealed class PairingStores
{
    private readonly IReadOnlyList<IPairingStore> _stores;

    /// <summary>
    /// Initializes a new instance of the <see cref="PairingStores"/> class over
    /// the stores this plugin owns.
    /// </summary>
    /// <param name="stores">The stores.</param>
    /// <exception cref="ArgumentNullException">There are no stores to ask.</exception>
    /// <remarks>
    /// An empty set is allowed and a missing one is not. A plugin owning no store
    /// yet answers that it holds nothing, which is true; a caller that forgot to
    /// hand the stores over answers the same thing and is wrong, so the two are
    /// told apart here rather than at the surface that shows the number.
    /// </remarks>
    public PairingStores(IEnumerable<IPairingStore> stores)
    {
        ArgumentNullException.ThrowIfNull(stores);

        _stores = new ReadOnlyCollection<IPairingStore>(stores.ToList());
    }

    /// <summary>
    /// What every store holds for one pairing.
    /// </summary>
    /// <param name="pairingId">The pairing to ask about.</param>
    /// <returns>The report.</returns>
    public PairingReport Report(Guid pairingId)
    {
        return new PairingReport
        {
            PairingId = pairingId,
            Holdings = _stores.Select(store => store.Holding(pairingId)).ToList(),
        };
    }

    /// <summary>
    /// Removes everything every store holds for one pairing, and answers with
    /// what was removed.
    /// </summary>
    /// <param name="pairingId">The pairing whose rows are to go.</param>
    /// <returns>The report as it stood immediately before the removal.</returns>
    /// <exception cref="StoreRemovedADifferentNumberException">A store removed a different number of rows than it had just reported holding.</exception>
    /// <remarks>
    /// The report is taken first and answered with afterwards, because what an
    /// operator needs after a removal is what went rather than what is left, and
    /// what is left is nothing.
    /// <para>
    /// The two numbers are compared rather than trusted. A store answering one
    /// count and removing another is either deleting rows of a pairing nobody
    /// asked about or leaving rows an operator has been told are gone. The second
    /// is the worse of the two because it is a false assurance, and this is the
    /// only moment at which both numbers exist.
    /// </para>
    /// </remarks>
    public PairingReport Remove(Guid pairingId)
    {
        var removing = Report(pairingId);

        for (var n = 0; n < _stores.Count; n++)
        {
            var removed = _stores[n].Remove(pairingId);

            if (removed != removing.Holdings[n].Count)
            {
                // On one line because the register in the suite keys a refusal
                // site by the line of code that refuses, and a throw spelled
                // across four lines is a site that scan cannot name.
                throw new StoreRemovedADifferentNumberException(removing.Holdings[n].Store, removing.Holdings[n].Count, removed);
            }
        }

        return removing;
    }
}
