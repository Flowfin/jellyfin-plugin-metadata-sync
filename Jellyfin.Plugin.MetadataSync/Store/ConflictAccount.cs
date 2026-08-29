using System;
using System.Collections.Generic;
using Jellyfin.Plugin.MetadataSync.Conflicts;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// What this plugin can still say about one pairing's decisions, and what it
/// can no longer say.
/// </summary>
/// <remarks>
/// The three numbers travel with the rows because a list of rows on its own
/// reads as the whole account. An operator who exports a log, finds the field
/// they are arguing about missing from it and concludes it was never decided is
/// wrong in the one direction this register cannot afford, and the difference
/// between that and the truth is a count nobody showed them.
/// <para>
/// The bound is carried for the same reason. A reader who has the file and not
/// this build cannot otherwise tell an account that lost nothing from one that
/// was never near the bound, and the number is what says which.
/// </para>
/// </remarks>
public sealed class ConflictAccount
{
    /// <summary>
    /// Gets the pairing the decisions were taken under.
    /// </summary>
    public required Guid Pairing { get; init; }

    /// <summary>
    /// Gets the decisions still held, oldest first.
    /// </summary>
    public required IReadOnlyList<ConflictEntry> Entries { get; init; }

    /// <summary>
    /// Gets how many decisions the bound has pushed out of this account.
    /// </summary>
    /// <remarks>
    /// A count and never a description. What was dropped is gone, and this says
    /// how much of it there was so that a reader is told the account is
    /// incomplete rather than being handed what is left as though it were all of
    /// it.
    /// </remarks>
    public required int Dropped { get; init; }

    /// <summary>
    /// Gets how many lines of the store could not be read back.
    /// </summary>
    /// <remarks>
    /// A store-wide number rather than this pairing's own. A line that cannot be
    /// read cannot be attributed to a pairing either, which is what makes it
    /// unreadable, so narrowing it would mean inventing an attribution for
    /// exactly the lines that have none.
    /// </remarks>
    public required int Unreadable { get; init; }

    /// <summary>
    /// Gets how many decisions the store keeps per pairing.
    /// </summary>
    public required int BoundedAt { get; init; }
}
