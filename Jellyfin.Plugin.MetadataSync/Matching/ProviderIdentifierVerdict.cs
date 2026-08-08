namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// What comparing two items' provider identifiers established.
/// </summary>
public enum ProviderIdentifierVerdict
{
    /// <summary>
    /// No provider is present on both sides, so nothing was compared. This is
    /// not a statement that the items are different; it is a statement that
    /// these identifiers cannot decide.
    /// </summary>
    NoBasis = 0,

    /// <summary>
    /// At least one provider is present on both sides and every such provider
    /// agrees.
    /// </summary>
    Match = 1,

    /// <summary>
    /// At least one provider is present on both sides and disagrees. This wins
    /// over any number of providers that agree, because two identifiers naming
    /// different works is evidence and two naming the same one is not evidence
    /// against it.
    /// </summary>
    Disagreement = 2,
}
