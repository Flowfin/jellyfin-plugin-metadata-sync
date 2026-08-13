using System;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// Thrown when a plan row says to write a value the write path cannot turn into
/// what the server holds.
/// </summary>
/// <remarks>
/// It is separate from the refusals in <c>Fields</c> because it says something
/// different to a caller. A field that is locked or has no row is a decision
/// about whether the value may travel at all, and it is taken while the plan is
/// being made. This one says that decision was taken, the value may travel, and
/// the row as written does not describe a value: a year that is not a number, a
/// date in a spelling nothing declared, or a field this path has no writer for.
/// That is a defect in whatever produced the row rather than an operator's claim
/// on their library, so it is loud rather than skipped.
/// </remarks>
public sealed class WriteRefusedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WriteRefusedException"/> class.
    /// </summary>
    public WriteRefusedException()
        : base("The plan row does not describe a value this path can write.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WriteRefusedException"/> class.
    /// </summary>
    /// <param name="message">What was asked for and why it was refused.</param>
    public WriteRefusedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WriteRefusedException"/> class.
    /// </summary>
    /// <param name="message">What was asked for and why it was refused.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public WriteRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
