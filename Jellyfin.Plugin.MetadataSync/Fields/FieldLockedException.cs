using System;

namespace Jellyfin.Plugin.MetadataSync.Fields;

/// <summary>
/// Thrown when something asks to write a field an operator has locked, or any
/// field at all on an item they have locked outright.
/// </summary>
/// <remarks>
/// It is a separate type from <see cref="FieldNotDeclaredException"/> because
/// the two say opposite things to a caller. A field with no row is a defect in
/// this plugin's own declaration and stays refused until somebody argues a row
/// for it. A locked field is a declared field that this particular operator has
/// claimed on this particular item, and the same field on the item beside it
/// moves normally. Collapsing them would make a lock read like a missing row,
/// and the recording that a lock refusal owes is not the recording a missing row
/// owes.
/// </remarks>
public sealed class FieldLockedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldLockedException"/> class.
    /// </summary>
    public FieldLockedException()
        : base("The operator has locked that field on the receiving item.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldLockedException"/> class.
    /// </summary>
    /// <param name="message">What was asked for and which lock refused it.</param>
    public FieldLockedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldLockedException"/> class.
    /// </summary>
    /// <param name="message">What was asked for and which lock refused it.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public FieldLockedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
