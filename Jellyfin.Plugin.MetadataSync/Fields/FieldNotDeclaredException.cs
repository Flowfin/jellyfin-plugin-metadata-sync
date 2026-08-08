using System;

namespace Jellyfin.Plugin.MetadataSync.Fields;

/// <summary>
/// Thrown when something asks for a field the register does not declare, or
/// declares as one that does not move. The refusal is an exception rather than a
/// false return because a caller that ignores a return value writes the field
/// anyway, and writing a field nobody declared is the failure this whole
/// register exists against.
/// </summary>
public sealed class FieldNotDeclaredException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldNotDeclaredException"/> class.
    /// </summary>
    public FieldNotDeclaredException()
        : base("The field register does not declare that field.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldNotDeclaredException"/> class.
    /// </summary>
    /// <param name="message">What was asked for and why it was refused.</param>
    public FieldNotDeclaredException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldNotDeclaredException"/> class.
    /// </summary>
    /// <param name="message">What was asked for and why it was refused.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public FieldNotDeclaredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
