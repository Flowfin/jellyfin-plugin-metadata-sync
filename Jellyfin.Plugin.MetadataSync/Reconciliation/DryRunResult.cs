namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// What a pass over one request would do, without any of it having been done.
/// </summary>
/// <remarks>
/// It carries the plan and one number the plan cannot: how many items this pass
/// would never consider, because an earlier pass over the same pairing had
/// already finished with them. A plan is what is left to decide, so a reader
/// handed only the plan cannot tell a library with nothing to change from one an
/// interrupted pass has already been most of the way through.
/// </remarks>
public sealed class DryRunResult
{
    /// <summary>
    /// Gets the plan a pass would carry out.
    /// </summary>
    public required Plan Plan { get; init; }

    /// <summary>
    /// Gets how many items an earlier pass over this pairing had already
    /// finished with, so this one would not consider them.
    /// </summary>
    public required int ItemsAlreadyDone { get; init; }
}
