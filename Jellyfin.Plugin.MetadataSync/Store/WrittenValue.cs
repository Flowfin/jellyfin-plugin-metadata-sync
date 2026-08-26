namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// One write this plugin made: the value it wrote, and the value that was on
/// the item immediately before it.
/// </summary>
/// <remarks>
/// The two travel together because separating them is what makes the second one
/// unanswerable. A store keeping the written values alone can be asked what this
/// plugin put there and never what it replaced, and the value it replaced is the
/// one an operator asks about: it is the entry the conflict log has to show
/// beside a decision, and it is the only thing #64 has to put back.
/// <para>
/// <see cref="Previous"/> is the value that was there rather than the value this
/// plugin wrote last time. Those are the same value on a field nobody here has
/// touched between two passes and they are different values on the field this
/// whole record exists for, which is one an operator edited after this plugin
/// wrote it. Deriving one from the other would lose exactly the edit.
/// </para>
/// <para>
/// Null means the field held nothing, on either member. It does not mean nothing
/// was recorded: an absence of a record is an empty history rather than an entry
/// carrying nulls, which is the distinction
/// <see cref="IWrittenValues.LastWritten"/> cannot make on its own and
/// <see cref="IWrittenValues.History"/> can.
/// </para>
/// </remarks>
public sealed class WrittenValue
{
    /// <summary>
    /// Gets the value this plugin wrote, or null where the write cleared the
    /// field.
    /// </summary>
    public required string? Value { get; init; }

    /// <summary>
    /// Gets the value that was on the item before this plugin wrote, or null
    /// where the field held nothing.
    /// </summary>
    public required string? Previous { get; init; }
}
