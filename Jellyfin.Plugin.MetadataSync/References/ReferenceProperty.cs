namespace Jellyfin.Plugin.MetadataSync.References;

/// <summary>
/// The four ways two spellings of one reference can differ, each of which the
/// table answers per kind.
/// </summary>
/// <remarks>
/// The set is closed and the table has to answer all of it for every kind, so a
/// property nobody thought about is a red suite rather than a silent default.
/// A difference that is none of these is a difference in the letters
/// themselves, which needs no row: two different names are two different
/// things.
/// </remarks>
public enum ReferenceProperty
{
    /// <summary>
    /// Space before, after, or doubled inside the value.
    /// </summary>
    Whitespace,

    /// <summary>
    /// Capitalisation.
    /// </summary>
    Case,

    /// <summary>
    /// Marks over or under a letter, compared after the value is put in one
    /// composed form, so two encodings of one character are never two
    /// characters.
    /// </summary>
    Accents,

    /// <summary>
    /// Punctuation, and the space it stands in for. A hyphen between two words
    /// and a space between the same two words differ in exactly this and
    /// nothing else.
    /// </summary>
    Punctuation,
}
