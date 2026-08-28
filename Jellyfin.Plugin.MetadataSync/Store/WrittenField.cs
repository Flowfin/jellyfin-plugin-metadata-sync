using System;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// One field on one item that this plugin has a record of writing.
/// </summary>
/// <remarks>
/// It is a key rather than a value: what was written and what it replaced are
/// <see cref="WrittenValue"/>'s, and this says only which field on which item to
/// ask about. That separation is what lets a caller find everything one pairing
/// touched without reading a library, which is what a revert needs and what an
/// answer that took an hour on a large library would not be.
/// </remarks>
public readonly record struct WrittenField
{
    /// <summary>
    /// Gets the item on this server.
    /// </summary>
    public required Guid Item { get; init; }

    /// <summary>
    /// Gets the field, named as the register names it.
    /// </summary>
    public required string Field { get; init; }
}
