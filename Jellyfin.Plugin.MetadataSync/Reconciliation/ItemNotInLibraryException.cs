using System;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// Thrown when the item a plan names is not in the library at the moment the
/// plan is carried out.
/// </summary>
/// <remarks>
/// A type of its own rather than a message, because this is one of the two
/// refusals on the write path that are nobody's defect. The item was there when
/// the plan was made and it is not there now, which is an operator removing
/// something between the two halves of a pass and is a normal event on a library
/// somebody uses.
/// <para>
/// It is a deferral rather than a failure, which is what the base type means. The
/// difference from its sibling is what the next pass finds: an item that moved is
/// still there and is written on the next pass, and an item that has gone is
/// gone, so what is deferred is the decision about it rather than the write.
/// </para>
/// </remarks>
public sealed class ItemNotInLibraryException : DeferredItemException
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
