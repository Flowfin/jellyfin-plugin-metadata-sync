namespace Jellyfin.Plugin.MetadataSync.References;

/// <summary>
/// The kinds of reference this plugin resolves. A reference is a field whose
/// value names something the server holds separately, so writing it means
/// finding that thing here or making it.
/// </summary>
/// <remarks>
/// Tags and production locations are not here, and their absence is the
/// decision rather than an omission. The field register declares both as plain
/// strings the server builds no entity from, so writing one creates nothing on
/// the receiving side and there is nothing to resolve.
/// </remarks>
public enum ReferenceKind
{
    /// <summary>
    /// A genre, which the receiving server holds as an entity an operator
    /// browses their library by.
    /// </summary>
    Genre,

    /// <summary>
    /// A studio, held the same way.
    /// </summary>
    Studio,

    /// <summary>
    /// A person, reached through the library rather than held on the item, and
    /// the kind where a wrong answer is a wrong answer about somebody.
    /// </summary>
    Person,
}
