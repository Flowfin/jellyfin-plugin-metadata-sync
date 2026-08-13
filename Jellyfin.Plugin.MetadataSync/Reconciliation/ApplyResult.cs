namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// What carrying a plan out actually did.
/// </summary>
/// <remarks>
/// Counted from the plan the applier was handed rather than reported back by
/// the target, so these numbers say what was asked for. What a server did with
/// each request is the target's to report and is not here.
/// </remarks>
public readonly record struct ApplyResult
{
    /// <summary>
    /// Gets how many items were handed to the target.
    /// </summary>
    public required int ItemsWritten { get; init; }

    /// <summary>
    /// Gets how many fields those items carried between them.
    /// </summary>
    public required int FieldsWritten { get; init; }

    /// <summary>
    /// Gets how many items the plan carried that write nothing.
    /// </summary>
    /// <remarks>
    /// Counted rather than left implicit. A pass over ten thousand items that
    /// writes none of them and a pass that was handed nothing are different
    /// events, and a result that could not tell them apart would read as the
    /// second in both cases.
    /// </remarks>
    public required int ItemsPassedOver { get; init; }

    /// <summary>
    /// Gets how many items the plan wanted to write and the write path handed
    /// back, because something else had written them or they had gone.
    /// </summary>
    /// <remarks>
    /// Counted apart from the two above because it means something else entirely.
    /// An item passed over is one the plan decided against, and it will be
    /// decided against again. An item deferred is one the plan decided to write
    /// and this pass did not, so the work is outstanding rather than done, and a
    /// pass reporting the two as one number would report a library as synced that
    /// is not.
    /// </remarks>
    public required int ItemsDeferred { get; init; }
}
