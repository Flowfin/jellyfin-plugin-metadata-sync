namespace Jellyfin.Plugin.MetadataSync.References;

/// <summary>
/// One row of the comparison table: what a difference in one property means for
/// one kind of reference, and the sentence it is argued by.
/// </summary>
public sealed class ComparisonRule
{
    internal ComparisonRule(ReferenceKind kind, ReferenceProperty property, ReferenceAnswer answer, string reason)
    {
        Kind = kind;
        Property = property;
        Answer = answer;
        Reason = reason;
    }

    /// <summary>
    /// Gets the kind of reference this row is about.
    /// </summary>
    public ReferenceKind Kind { get; }

    /// <summary>
    /// Gets the way two spellings differ.
    /// </summary>
    public ReferenceProperty Property { get; }

    /// <summary>
    /// Gets what that difference means.
    /// </summary>
    public ReferenceAnswer Answer { get; }

    /// <summary>
    /// Gets the sentence the row is argued by. The three kinds answer alike on
    /// three of the four properties today, and the reasons are what say whether
    /// that is one argument or three that happen to agree.
    /// </summary>
    public string Reason { get; }
}
