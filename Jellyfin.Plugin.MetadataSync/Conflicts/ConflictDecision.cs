namespace Jellyfin.Plugin.MetadataSync.Conflicts;

/// <summary>
/// What the resolver decided for one field on one item, and which declared rule
/// decided it.
/// </summary>
/// <remarks>
/// The rule travels with the outcome because an outcome on its own cannot be
/// argued with. An operator reading a conflict log wants to know that their
/// overview stayed because they locked the item, and a maintainer reading the
/// same entry wants to know which row of the table to change if that was wrong.
/// Both need the name.
/// </remarks>
public readonly record struct ConflictDecision
{
    /// <summary>
    /// Gets what happens to the field.
    /// </summary>
    public required ConflictOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the declared rule that produced the outcome, or null where the
    /// table ran out and no rule fired at all.
    /// </summary>
    /// <remarks>
    /// Null is the residual and it is always a refusal. It is not an error and
    /// it is not a rule with an empty name: it is the state the rule set is
    /// allowed to reach, which is why the table can run out instead of ending
    /// in a row that answers everything. What that state owes an operator is
    /// #45.
    /// </remarks>
    public required ConflictRule? Rule { get; init; }

    /// <summary>
    /// Gets the value this server holds once the decision is carried out.
    /// </summary>
    /// <remarks>
    /// It is always one of the two values handed in, never a third built out of
    /// them. On <see cref="ConflictOutcome.TakePeer"/> it is the peer's value,
    /// and that is the only outcome that writes anything at all. On
    /// <see cref="ConflictOutcome.KeepLocal"/> and
    /// <see cref="ConflictOutcome.Refuse"/> nothing is written and this is this
    /// server's own value, unchanged.
    /// </remarks>
    public required string? Value { get; init; }
}
