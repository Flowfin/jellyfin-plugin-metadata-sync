using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// Everything a parent-plus-ordinal item is identified by, and nothing else.
/// </summary>
/// <remarks>
/// What is absent is the point of the type. There is no path, no filename, no
/// directory, no size and no hash, so a rule that read one could not be written
/// against this shape without changing the shape first, and the change would be
/// the thing a reader argues with. `matching.md` is where that refusal is argued.
/// <para>
/// There is no title and no air date either, and those are a narrower refusal
/// with their own reason. Both are metadata this plugin moves, so resolving on
/// one would decide that two items are the same from a value a previous pass may
/// have written, and the second pass would then agree with itself.
/// </para>
/// </remarks>
public sealed class OrdinalIdentity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrdinalIdentity"/> class.
    /// </summary>
    /// <param name="parentIdentifiers">The parent's provider identifiers, as the parent's dictionary spells them.</param>
    /// <param name="season">The season this item is counted within, or null where it carries no season.</param>
    /// <param name="number">The item's number inside that season, or null where it carries none.</param>
    /// <param name="lastNumber">The last number a range covers, or null where the item is one episode.</param>
    /// <param name="absoluteNumber">The item's absolute number, or null where it carries none.</param>
    public OrdinalIdentity(
        IReadOnlyDictionary<string, string> parentIdentifiers,
        int? season,
        int? number,
        int? lastNumber,
        int? absoluteNumber)
    {
        ArgumentNullException.ThrowIfNull(parentIdentifiers);

        ParentIdentifiers = parentIdentifiers;
        Season = season;
        Number = number;
        LastNumber = lastNumber;
        AbsoluteNumber = absoluteNumber;
    }

    /// <summary>
    /// Gets the parent's provider identifiers, which are what the first step
    /// resolves on.
    /// </summary>
    public IReadOnlyDictionary<string, string> ParentIdentifiers { get; }

    /// <summary>
    /// Gets the season this item is counted within, or null where it carries no
    /// season. Zero is a season the server uses and is not the same as none.
    /// </summary>
    public int? Season { get; }

    /// <summary>
    /// Gets the item's number inside its season, or null where it carries none.
    /// </summary>
    public int? Number { get; }

    /// <summary>
    /// Gets the last number a range covers, or null where the item is one
    /// episode. A file holding two episodes carries both ends.
    /// </summary>
    public int? LastNumber { get; }

    /// <summary>
    /// Gets the item's absolute number, or null where it carries none. It is
    /// read only to say which of two refusals applies, and never to resolve on.
    /// </summary>
    public int? AbsoluteNumber { get; }

    /// <summary>
    /// Gets a value indicating whether this item is numbered by a season and a
    /// number inside it, which is the only numbering this plugin resolves on.
    /// </summary>
    public bool IsNumberedWithinASeason => Season is not null && Number is not null;

    /// <summary>
    /// Gets a value indicating whether this item's ordinal is a range rather
    /// than a number.
    /// </summary>
    public bool CoversARange => LastNumber is not null;
}
