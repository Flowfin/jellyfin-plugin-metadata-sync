using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Store;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// A store that holds what it is given, for the cases about the report rather
/// than about any particular store.
/// </summary>
/// <remarks>
/// A second store does not exist in this plugin yet, and the report is the one
/// type whose whole job is to be right about however many there are. So the
/// cases that ask what a report does with two stores are arranged with one real
/// store and this, rather than waiting for the conflict log to be built.
/// </remarks>
internal sealed class AStoreOfItsOwn : IPairingStore
{
    private readonly Dictionary<Guid, List<string>> _rows = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AStoreOfItsOwn"/> class.
    /// </summary>
    /// <param name="name">The name it reports itself under.</param>
    /// <param name="held">What it says it holds.</param>
    public AStoreOfItsOwn(string name, string held)
    {
        Name = name;
        Held = held;
    }

    /// <summary>
    /// Gets the name this store reports itself under.
    /// </summary>
    public string Name { get; }

    /// <inheritdoc />
    public string Held { get; }

    /// <summary>
    /// Gets how many times this store was asked to remove something.
    /// </summary>
    public int RemovalsAsked { get; private set; }

    /// <summary>
    /// Gets or sets what this store answers when it is asked to remove, in place
    /// of the number of rows it actually let go of. Null is the honest answer.
    /// </summary>
    /// <remarks>
    /// It exists for one case: a store whose two numbers disagree. That is the
    /// arrangement the report refuses, and there is no honest store that
    /// produces it.
    /// </remarks>
    public int? AnswersRemovalWith { get; set; }

    /// <summary>
    /// Puts a row in, under a pairing.
    /// </summary>
    /// <param name="pairingId">The pairing.</param>
    /// <param name="row">The row.</param>
    public void Add(Guid pairingId, string row)
    {
        if (!_rows.TryGetValue(pairingId, out var rows))
        {
            rows = new List<string>();
            _rows[pairingId] = rows;
        }

        rows.Add(row);
    }

    /// <inheritdoc />
    public PairingHolding Holding(Guid pairingId)
    {
        return new PairingHolding
        {
            Store = Name,
            Held = Held,
            Rows = _rows.TryGetValue(pairingId, out var rows)
                ? new ReadOnlyCollection<string>(rows.ToList())
                : Array.Empty<string>(),
        };
    }

    /// <inheritdoc />
    public int Remove(Guid pairingId)
    {
        RemovalsAsked++;

        var removed = _rows.TryGetValue(pairingId, out var rows) ? rows.Count : 0;
        _rows.Remove(pairingId);

        return AnswersRemovalWith ?? removed;
    }

    /// <summary>
    /// A row, spelled so a case can tell one from another.
    /// </summary>
    /// <param name="n">Which one.</param>
    /// <returns>The row.</returns>
    public static string Row(int n) =>
        string.Format(CultureInfo.InvariantCulture, "row {0} of a store of its own", n);
}
