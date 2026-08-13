using System;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// Thrown when the item a plan names is not in the library at the moment the
/// plan is carried out.
/// </summary>
/// <remarks>
/// A type of its own rather than a message, because this is the one refusal on
/// the write path that is nobody's defect. The item was there when the plan was
/// made and it is not there now, which is an operator removing something between
/// the two halves of a pass and is a normal event on a library somebody uses.
/// <para>
/// It is a refusal here and it should not stay one. #41 turns exactly this case
/// into a deferral the next pass picks up, and it can tell this apart from a
/// malformed row by the type rather than by reading a message.
/// </para>
/// </remarks>
public sealed class ItemNotInLibraryException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ItemNotInLibraryException"/> class.
    /// </summary>
    public ItemNotInLibraryException()
        : base("The item this plan is about is not in the library any more.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemNotInLibraryException"/> class.
    /// </summary>
    /// <param name="message">Which item was asked for.</param>
    public ItemNotInLibraryException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemNotInLibraryException"/> class.
    /// </summary>
    /// <param name="message">Which item was asked for.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public ItemNotInLibraryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
