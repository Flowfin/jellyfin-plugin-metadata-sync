using System.Collections.Generic;

namespace Jellyfin.Plugin.MetadataSync.Fields;

/// <summary>
/// One declared field. A field with no row of its own does not move, and the
/// mover refuses it rather than guessing.
/// </summary>
public sealed class FieldRow
{
    internal FieldRow(
        string field,
        string? declaredOn,
        string? reachedBy,
        string kindGroup,
        IReadOnlyList<string> kinds,
        bool moves,
        FieldClass fieldClass,
        bool fromTheFile,
        string reason)
    {
        Field = field;
        DeclaredOn = declaredOn;
        ReachedBy = reachedBy;
        KindGroup = kindGroup;
        Kinds = kinds;
        Moves = moves;
        Class = fieldClass;
        FromTheFile = fromTheFile;
        Reason = reason;
    }

    /// <summary>
    /// Gets the field, named as the server names it on the item type.
    /// </summary>
    public string Field { get; }

    /// <summary>
    /// Gets the item type that declares this field as a property, or null where
    /// the field is not a property on an item at all.
    /// </summary>
    public string? DeclaredOn { get; }

    /// <summary>
    /// Gets the server interface a field that is not an item property is reached
    /// through. Null wherever <see cref="DeclaredOn"/> is set.
    /// </summary>
    public string? ReachedBy { get; }

    /// <summary>
    /// Gets the name of the kind group this row applies to.
    /// </summary>
    public string KindGroup { get; }

    /// <summary>
    /// Gets the item kinds this row applies to, expanded from the group.
    /// </summary>
    public IReadOnlyList<string> Kinds { get; }

    /// <summary>
    /// Gets a value indicating whether this field may move between two paired
    /// servers at all.
    /// </summary>
    public bool Moves { get; }

    /// <summary>
    /// Gets what a wrong value in this field costs.
    /// </summary>
    public FieldClass Class { get; }

    /// <summary>
    /// Gets a value indicating whether the value is derived from the media file
    /// this server holds. A field derived from the file describes this copy, so
    /// the peer's value describes the peer's copy and never moves.
    /// </summary>
    public bool FromTheFile { get; }

    /// <summary>
    /// Gets the sentence this row is argued by. A row with no reason is a row
    /// nobody can disagree with later.
    /// </summary>
    public string Reason { get; }
}
