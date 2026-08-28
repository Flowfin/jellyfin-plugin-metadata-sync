namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// What a pass that ran to the end did.
/// </summary>
/// <remarks>
/// It exists only where a pass finished. A pass an operator stopped throws where
/// it was stopped and answers nothing, and what it got through is on the disk
/// rather than in a value nobody received; turning an interruption into a result
/// carrying the items already written is the pass's own bound, which is #37.
/// <para>
/// The four counts of <see cref="ApplyResult"/> are the same four numbers summed
/// over the items this pass applied, and <see cref="ItemsAlreadyDone"/> is the
/// one number a plan cannot carry: how many items this pass did not consider
/// because an earlier interrupted pass over the same pairing had already
/// finished with them. It is stated rather than folded into the items passed
/// over, because an item nobody looked at and an item looked at and left alone
/// are different facts about a library.
/// </para>
/// </remarks>
public readonly record struct PassResult
{
    /// <summary>
    /// Gets how many items were written.
    /// </summary>
    public required int ItemsWritten { get; init; }

    /// <summary>
    /// Gets how many fields were written across those items.
    /// </summary>
    public required int FieldsWritten { get; init; }

    /// <summary>
    /// Gets how many items this pass considered and had nothing to write to.
    /// </summary>
    public required int ItemsPassedOver { get; init; }

    /// <summary>
    /// Gets how many items were deferred because something else was writing
    /// them.
    /// </summary>
    /// <remarks>
    /// A deferred item is not recorded as finished with, so the pass that runs
    /// next reaches it again. That is the difference between an item this pass
    /// decided about and one it was kept away from.
    /// </remarks>
    public required int ItemsDeferred { get; init; }

    /// <summary>
    /// Gets how many items an earlier interrupted pass had already finished
    /// with, and this one therefore did not consider.
    /// </summary>
    public required int ItemsAlreadyDone { get; init; }
}
