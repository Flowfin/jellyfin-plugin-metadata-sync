using System;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// An item this pass will not write, for a reason that is nobody's defect and
/// that the next pass may find gone.
/// </summary>
/// <remarks>
/// A base rather than a message, so the half of a pass that acts can tell a
/// deferral from a defect without reading text. Everything under this type means
/// the same thing to a caller: nothing was written to that item, the pass carries
/// on with the rest, and the item is counted as deferred rather than failed. A
/// plan row that does not describe a value is not one of these, because that is a
/// defect in whatever produced the row and passing over it quietly would hide it.
/// <para>
/// The distinction is what keeps a pass honest on a library somebody uses. An
/// operator editing one film while a sync runs is the ordinary case, and a pass
/// that reported it as a failure would teach an operator to ignore failures.
/// </para>
/// <para>
/// It carries no constructor without a message. Every type under it says which
/// item was passed over and why, and a base that could be built saying neither
/// would be a line no arrangement reaches, which reads in a coverage run exactly
/// like a branch nobody thought about.
/// </para>
/// </remarks>
public abstract class DeferredItemException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeferredItemException"/> class.
    /// </summary>
    /// <param name="message">Which item was passed over, and why.</param>
    protected DeferredItemException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeferredItemException"/> class.
    /// </summary>
    /// <param name="message">Which item was passed over, and why.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    protected DeferredItemException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
