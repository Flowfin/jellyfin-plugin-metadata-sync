using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Store;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// A store that keeps what it is told in memory, so a case about what a pass
/// wrote can read it back without a directory.
/// </summary>
/// <remarks>
/// It applies the same bound as the real one and in the same direction, because
/// a double that kept everything would let a case pass that the store on a disk
/// would fail. What it deliberately does not do is touch a file: whether a
/// record survives the process that wrote it is
/// <see cref="WrittenValuesTests"/>, against the real store, and a double
/// cannot answer it.
/// </remarks>
internal sealed class RecordingWrittenValues : IWrittenValues
{
    private readonly Dictionary<(Guid Pairing, Guid Item, string Field), List<WrittenValue>> _held = new();

    /// <summary>
    /// Gets every value recorded, in the order it was recorded, so a case can
    /// assert the order of a pass as well as its result.
    /// </summary>
    public Collection<(Guid Pairing, Guid Item, string Field, string? Value, string? Previous)> Recorded { get; } = new();

    /// <inheritdoc />
    public void Record(Guid pairingId, Guid itemId, string field, string? value, string? previousValue)
    {
        Recorded.Add((pairingId, itemId, field, value, previousValue));

        var key = (pairingId, itemId, field);

        if (!_held.TryGetValue(key, out var values))
        {
            values = new List<WrittenValue>();
            _held[key] = values;
        }

        values.Add(new WrittenValue { Value = value, Previous = previousValue });

        while (values.Count > WrittenValues.Bound)
        {
            values.RemoveAt(0);
        }
    }

    /// <inheritdoc />
    public string? LastWritten(Guid pairingId, Guid itemId, string field)
    {
        return _held.TryGetValue((pairingId, itemId, field), out var values) && values.Count > 0
            ? values[^1].Value
            : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<WrittenValue> History(Guid pairingId, Guid itemId, string field)
    {
        return _held.TryGetValue((pairingId, itemId, field), out var values)
            ? new ReadOnlyCollection<WrittenValue>(values.ToList())
            : Array.Empty<WrittenValue>();
    }
}
