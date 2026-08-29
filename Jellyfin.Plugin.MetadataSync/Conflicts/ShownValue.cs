namespace Jellyfin.Plugin.MetadataSync.Conflicts;

/// <summary>
/// One of the two values a conflict log entry carries, as the entry shows it,
/// with the fact that it was cut carried beside it rather than left to be
/// noticed.
/// </summary>
/// <remarks>
/// A metadata field holds whatever an operator or a provider put in it, and an
/// overview runs to paragraphs. An entry that showed the whole of both sides
/// would be unreadable on the field it is most needed for, and one that showed
/// half a value without saying so would be worse: an operator comparing two
/// truncated overviews would read a difference into a cut that the rules never
/// saw.
/// <para>
/// So the cut is a property of the value rather than an ellipsis appended to
/// it. Nothing here formats anything for a screen, and no marker is added to
/// the text: a marker inside the text is indistinguishable from a value that
/// ends in one, which is the failure this shape exists against.
/// </para>
/// <para>
/// This is a display bound and it is not the payload bound. What a message may
/// weigh on the wire is the pairing plugin's number and is #24's to read at run
/// time; this one is about what fits in a row somebody reads.
/// </para>
/// </remarks>
public readonly record struct ShownValue
{
    /// <summary>
    /// How many characters of a value an entry shows.
    /// </summary>
    /// <remarks>
    /// Two hundred rather than a rounder number for a stated reason: a conflict
    /// log is read as a table of rows, and the shortest field this plugin moves
    /// is a name while the longest is an overview. Two hundred characters is
    /// about two lines of one, which is enough to see which of two descriptions
    /// is which and short enough that a row stays a row. It is a choice rather
    /// than a measurement, and it is declared once here so that a document
    /// stating it is a rendering of this number and not a second copy of it.
    /// </remarks>
    public const int DisplayBound = 200;

    /// <summary>
    /// Gets the value as the entry shows it, cut to
    /// <see cref="DisplayBound"/> where it was longer, or null where the field
    /// held nothing.
    /// </summary>
    public required string? Text { get; init; }

    /// <summary>
    /// Gets a value indicating whether the text above is shorter than the value
    /// it came from.
    /// </summary>
    public required bool Truncated { get; init; }

    /// <summary>
    /// Takes a value off an item and says what an entry shows of it.
    /// </summary>
    /// <param name="value">The value as the rules were handed it, or null.</param>
    /// <returns>What the entry shows, and whether that is all of it.</returns>
    /// <remarks>
    /// A value is never repaired on the way in. Whitespace is kept, and so is a
    /// character with no glyph, because the rules read both as text an operator
    /// can have typed and an entry that tidied either would explain a decision
    /// the resolver did not make.
    /// <para>
    /// The cut is taken one short of the bound where the bound would fall
    /// between the two halves of one character. A string is counted in UTF-16
    /// code units and a character outside the basic plane is two of them, so a
    /// cut at the bound can leave the first half of one behind - which is not a
    /// shorter value, it is a value that is no longer text.
    /// </para>
    /// </remarks>
    public static ShownValue Of(string? value)
    {
        if (value is null || value.Length <= DisplayBound)
        {
            return new ShownValue { Text = value, Truncated = false };
        }

        var kept = DisplayBound;
        if (char.IsHighSurrogate(value[kept - 1]))
        {
            kept--;
        }

        return new ShownValue { Text = value[..kept], Truncated = true };
    }
}
