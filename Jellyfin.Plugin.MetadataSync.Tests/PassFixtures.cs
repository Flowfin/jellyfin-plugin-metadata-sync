using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Jellyfin.Plugin.MetadataSync.Store;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Where a pass is stopped, as a value a case names rather than as a comment.
/// </summary>
/// <remarks>
/// These are the boundaries a pass's loop has around one item, and they are the
/// ones a resume has to handle. There is no fourth: the loop reads the token,
/// applies the item, records it, and moves on, so anything else a process could
/// die at falls inside one of these three.
/// </remarks>
internal enum StoppedAt
{
    /// <summary>
    /// While the item was being written, so nothing was written and nothing was
    /// recorded.
    /// </summary>
    TheWrite,

    /// <summary>
    /// After the item was written and before the record that it is done, which
    /// is the boundary the ordering deliberately leaves open.
    /// </summary>
    AfterTheWriteBeforeTheRecord,

    /// <summary>
    /// After the record that the item is done, so the resume owes nothing for
    /// this item.
    /// </summary>
    AfterTheRecord,
}

/// <summary>
/// A record of how far a pass got, kept in memory.
/// </summary>
/// <remarks>
/// It is the shape of the real store and not the store: whether a record
/// survives the process that wrote it is <see cref="PassProgressTests"/>, and a
/// double cannot answer it. What this exists for is the other half, which is
/// what a pass does with the record while it is running.
/// </remarks>
internal class RecordingPassProgress : IPassProgress
{
    private readonly Dictionary<Guid, HashSet<Guid>> _done = new();

    /// <summary>
    /// Gets every item recorded, in the order it was recorded, so a case can
    /// assert the order a pass recorded in and not only the set it ended with.
    /// </summary>
    public Collection<(Guid Pairing, Guid Item)> Recorded { get; } = new();

    /// <summary>
    /// Gets how many times a pairing's progress was cleared.
    /// </summary>
    public int Clearings { get; private set; }

    /// <inheritdoc />
    public virtual void Completed(Guid pairingId, Guid itemId)
    {
        Recorded.Add((pairingId, itemId));

        if (!_done.TryGetValue(pairingId, out var items))
        {
            items = new HashSet<Guid>();
            _done[pairingId] = items;
        }

        items.Add(itemId);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> CompletedItems(Guid pairingId) =>
        _done.TryGetValue(pairingId, out var items)
            ? new ReadOnlyCollection<Guid>(items.Order().ToList())
            : Array.Empty<Guid>();

    /// <inheritdoc />
    public int Cleared(Guid pairingId)
    {
        Clearings++;

        if (!_done.TryGetValue(pairingId, out var items))
        {
            return 0;
        }

        _done.Remove(pairingId);

        return items.Count;
    }
}

/// <summary>
/// The same record, which stops the pass at one item instead of recording it.
/// </summary>
/// <remarks>
/// It stands in for the process dying, which is the event this whole mechanism
/// is for and the one thing a test cannot actually do to itself. Throwing where
/// the process would have stopped leaves the record and the target holding
/// exactly what they would have been holding, which is what the resume is then
/// asked about.
/// </remarks>
internal sealed class ProgressThatStops : RecordingPassProgress
{
    private readonly Guid _item;
    private readonly bool _afterRecording;
    private bool _spent;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressThatStops"/> class.
    /// </summary>
    /// <param name="item">The item to stop at.</param>
    /// <param name="afterRecording">Whether the record is made before the pass stops.</param>
    public ProgressThatStops(Guid item, bool afterRecording)
    {
        _item = item;
        _afterRecording = afterRecording;
    }

    /// <inheritdoc />
    /// <remarks>
    /// It stops once. The same record is handed to the pass that resumes,
    /// because the record is the one thing that survives the process, and a
    /// double that stopped again at the same item would be a second
    /// interruption rather than a resume.
    /// </remarks>
    public override void Completed(Guid pairingId, Guid itemId)
    {
        var here = !_spent && itemId == _item;

        if (here && !_afterRecording)
        {
            _spent = true;
            throw new OperationCanceledException();
        }

        base.Completed(pairingId, itemId);

        if (here)
        {
            _spent = true;
            throw new OperationCanceledException();
        }
    }
}

/// <summary>
/// A target that records what it was handed and stops the pass at one item.
/// </summary>
internal sealed class TargetThatStops : IPlanTarget
{
    private readonly Guid _item;

    /// <summary>
    /// Initializes a new instance of the <see cref="TargetThatStops"/> class.
    /// </summary>
    /// <param name="item">The item to stop at, or an empty identifier to write everything.</param>
    public TargetThatStops(Guid item)
    {
        _item = item;
    }

    /// <summary>
    /// Gets the items written, in the order they arrived.
    /// </summary>
    public Collection<ItemPlan> Written { get; } = new();

    /// <inheritdoc />
    public Task WriteAsync(ItemPlan item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.LocalItemId == _item)
        {
            // Thrown before anything is recorded, which is the write itself
            // being interrupted. The write path is all or nothing per item, so
            // an item stopped here holds what it held.
            throw new OperationCanceledException();
        }

        Written.Add(item);

        return Task.CompletedTask;
    }
}
