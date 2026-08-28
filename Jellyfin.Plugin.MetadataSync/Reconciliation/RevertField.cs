namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// One field on an item as it stands now.
/// </summary>
public readonly record struct RevertField
{
    /// <summary>
    /// Gets the field, named as the register names it.
    /// </summary>
    public required string Field { get; init; }

    /// <summary>
    /// Gets what the field holds now, or null where it holds nothing.
    /// </summary>
    /// <remarks>
    /// Null is a field holding nothing rather than a field nobody read. A caller
    /// that has not read a field leaves it out of the item, which is a different
    /// statement and produces a different count.
    /// </remarks>
    public required string? LocalValue { get; init; }
}
