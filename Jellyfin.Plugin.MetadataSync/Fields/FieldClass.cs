namespace Jellyfin.Plugin.MetadataSync.Fields;

/// <summary>
/// What a wrong value in a field costs, which is a different question from
/// whether the field may move at all.
/// </summary>
public enum FieldClass
{
    /// <summary>
    /// A wrong value mislabels one item and nothing else. This is the class that
    /// is safe to overwrite.
    /// </summary>
    Descriptive = 0,

    /// <summary>
    /// A wrong value changes how the server organises the library, or what a
    /// restricted account is allowed to see. Overwriting one of these is the
    /// case an operator wants to have seen before it happened.
    /// </summary>
    Structural = 1,
}
