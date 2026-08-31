namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// What a pass did, and whether it reached the end of the plan.
/// </summary>
/// <remarks>
/// TWO OF THE THREE WAYS A PASS CAN END PRODUCE ONE OF THESE AND THE THIRD DOES
/// NOT, and <see cref="Finished"/> is what separates the two that do. A pass that
/// reached the end of its plan answers with one, and so does a pass that stopped
/// at its time bound; a pass an operator cancelled throws where it was stopped
/// and answers nothing, because the caller asked for that and there is nobody
/// left to hand a result to.
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

    /// <summary>
    /// Gets a value indicating whether the pass reached the end of its plan.
    /// </summary>
    /// <remarks>
    /// False means the pass stopped at its time bound with items left in the
    /// plan. It is not a failure and nothing was lost: the record of what this
    /// pass finished with is kept, and the pass after it continues from there
    /// rather than reading the library again.
    /// <para>
    /// It is required like the five counts beside it, so a sixth member added the
    /// same way is a compile error at every construction rather than a default
    /// nobody set. A pass that always said one thing would satisfy a member with
    /// a default and satisfy nothing else, which is why the suite asserts it in
    /// both directions.
    /// </para>
    /// <para>
    /// The counts beside it are what the pass got through before it stopped,
    /// which is the value #37 says a stopped pass never handed anybody: what it
    /// did was on the disk and in nothing a caller received.
    /// </para>
    /// </remarks>
    public required bool Finished { get; init; }
}
