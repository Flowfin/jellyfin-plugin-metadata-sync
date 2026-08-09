using System.Collections.Generic;

namespace Jellyfin.Plugin.MetadataSync.References;

/// <summary>
/// What resolving one incoming reference established, with what it established
/// it against.
/// </summary>
/// <remarks>
/// Every outcome carries a sentence. An outcome without one is a row in a
/// register an operator cannot act on, and the whole reason a non-resolution is
/// an outcome rather than a silence is that somebody has to be able to read it.
/// </remarks>
public sealed class ReferenceResolution
{
    internal ReferenceResolution(ReferenceOutcome outcome, string? match, IReadOnlyList<string> candidates, string reason)
    {
        Outcome = outcome;
        Match = match;
        Candidates = candidates;
        Reason = reason;
    }

    /// <summary>
    /// Gets what was established.
    /// </summary>
    public ReferenceOutcome Outcome { get; }

    /// <summary>
    /// Gets the entry already here that the reference resolved to, spelled as
    /// this server spells it, or null where nothing was resolved.
    /// </summary>
    public string? Match { get; }

    /// <summary>
    /// Gets the entries that made the outcome undecidable, in the order this
    /// server holds them. Empty for every other outcome.
    /// </summary>
    public IReadOnlyList<string> Candidates { get; }

    /// <summary>
    /// Gets the sentence saying what happened, for the operator who reads the
    /// row rather than for the caller that branches on the outcome.
    /// </summary>
    public string Reason { get; }
}
