namespace Jellyfin.Plugin.MetadataSync.Conflicts;

/// <summary>
/// One declared conflict rule. A rule is a condition, the outcome it produces
/// and the sentence it is argued by, and it has no code of its own.
/// </summary>
public sealed class ConflictRule
{
    internal ConflictRule(string id, string condition, ConflictOutcome outcome, string reason)
    {
        Id = id;
        Condition = condition;
        Outcome = outcome;
        Reason = reason;
    }

    /// <summary>
    /// Gets the name this rule is referred to by, in the document, in the
    /// fixtures and in whatever the resolver reports having applied.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets what fires the rule, read with every rule above it not having
    /// fired.
    /// </summary>
    public string Condition { get; }

    /// <summary>
    /// Gets what the rule produces.
    /// </summary>
    public ConflictOutcome Outcome { get; }

    /// <summary>
    /// Gets the sentence the rule is argued by. A rule with no reason is a rule
    /// nobody can disagree with later, which is the state this whole document
    /// exists to avoid.
    /// </summary>
    public string Reason { get; }
}
