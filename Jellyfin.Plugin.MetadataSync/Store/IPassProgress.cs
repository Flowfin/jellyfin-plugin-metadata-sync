using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// How far a pass over one pairing has got, so a pass that was stopped can be
/// continued instead of started again.
/// </summary>
/// <remarks>
/// The unit is the item, because the unit of application is the item. A pass
/// writes all of an item's planned fields or none of them, so an item is either
/// done or untouched and there is no third state for this record to describe.
/// <para>
/// It holds progress and never a plan. A resumed pass re-derives: the items are
/// observed again and the plan for what is left is built again, because the
/// library and the peer both moved while nothing was running, and a stored plan
/// replayed afterwards is how a pass writes a value the peer has since changed.
/// What survives an interruption is therefore a set of identifiers and nothing
/// that could be obeyed.
/// </para>
/// <para>
/// It is emptied when a pass finishes rather than kept, so what is on the disk
/// is the progress of a pass that did not finish and of nothing else. A reader
/// finding rows here is reading an interrupted pass; a reader finding none is
/// reading a pairing whose last pass ran to the end, and those are the only two
/// states.
/// </para>
/// <para>
/// It is an interface for the reason <see cref="IWrittenValues"/> is one: a
/// caller can be given one that keeps nothing, and the half of a pass that
/// records can be arranged in a test with no disk in the room. There is exactly
/// one implementation that persists, and it is <see cref="PassProgress"/>.
/// </para>
/// </remarks>
public interface IPassProgress
{
    /// <summary>
    /// Records that a pass has finished with one item.
    /// </summary>
    /// <param name="pairingId">The pairing the pass is for.</param>
    /// <param name="itemId">The item on this server that is done.</param>
    /// <remarks>
    /// Called after the item has been written and after what was written has
    /// been recorded, never before either. The two orderings fail in opposite
    /// directions and only one of them is survivable: a record written first
    /// claims an item is done that a later step may refuse, and a resume then
    /// skips an item nothing wrote. Written last, an interruption in between
    /// costs the item being written a second time, which writes the same values
    /// to the same fields.
    /// </remarks>
    void Completed(Guid pairingId, Guid itemId);

    /// <summary>
    /// The items an interrupted pass over one pairing has already finished
    /// with.
    /// </summary>
    /// <param name="pairingId">The pairing to ask about.</param>
    /// <returns>The items already done, empty where no pass was interrupted.</returns>
    /// <remarks>
    /// An empty answer is the ordinary one. It means the last pass over this
    /// pairing ran to the end, or that none has run, and this record cannot tell
    /// those apart because nothing about them differs: both are a pass with
    /// nothing to continue.
    /// </remarks>
    IReadOnlyCollection<Guid> CompletedItems(Guid pairingId);

    /// <summary>
    /// Lets go of the progress of one pairing's pass, because it finished.
    /// </summary>
    /// <param name="pairingId">The pairing whose pass is over.</param>
    /// <returns>How many items the finished pass had recorded.</returns>
    /// <remarks>
    /// Called by the pass that finished and by nothing else. Clearing it earlier
    /// would turn an interrupted pass into one that starts from the beginning,
    /// which is correct but costs the whole library again; not clearing it at
    /// all would make the next pass over the same pairing skip every item the
    /// last one wrote.
    /// </remarks>
    int Cleared(Guid pairingId);
}
