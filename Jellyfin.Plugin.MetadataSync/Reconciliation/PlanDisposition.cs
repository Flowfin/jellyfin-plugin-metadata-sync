namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// How far a field got before the plan decided about it, and which authority
/// answered.
/// </summary>
/// <remarks>
/// A plan that recorded only what it would write would be a plan an operator
/// cannot read: a field missing from it could be a field nobody looked at, a
/// field the register refuses, a field they excluded themselves, or a
/// difference no rule answers. Those have different actions, so they are
/// different members here and each row carries the sentence that goes with it.
/// <para>
/// Only <see cref="Decided"/> reaches the conflict rules. Everything above it
/// is settled before the two values are compared at all, which is why a field
/// that does not move never appears in the conflict log as a refusal: it was
/// never a conflict.
/// </para>
/// </remarks>
public enum PlanDisposition
{
    /// <summary>
    /// The register declares no such field, so nothing knows what it costs to
    /// get it wrong and nothing may write it.
    /// </summary>
    NotDeclared,

    /// <summary>
    /// The register declares the field and declares that it does not move.
    /// </summary>
    DoesNotMove,

    /// <summary>
    /// The register declares the field for other kinds of item and not for this
    /// one.
    /// </summary>
    OutsideTheKindGroup,

    /// <summary>
    /// The register allows the field to move and the operator excluded it.
    /// </summary>
    ExcludedByTheOperator,

    /// <summary>
    /// The field reached the conflict rules and they answered.
    /// </summary>
    Decided,
}
