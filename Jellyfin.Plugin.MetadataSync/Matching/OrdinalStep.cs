namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// Which of the two steps of a parent-plus-ordinal resolution answered.
/// </summary>
/// <remarks>
/// The two fail differently and an unmatched register that cannot tell them
/// apart is a register an operator cannot act on. A series that did not resolve
/// is one thing to fix and it fixes every episode under it; an episode that did
/// not resolve inside a series that did is a different thing entirely, and
/// telling an operator to look at the series when the series was never the
/// problem sends them at the wrong end of their library.
/// </remarks>
public enum OrdinalStep
{
    /// <summary>
    /// The first step: the parent, resolved by its own provider identifiers.
    /// Nothing under a parent that did not resolve is looked at.
    /// </summary>
    Parent,

    /// <summary>
    /// The second step: the ordinal, decided inside the resolved parent. Every
    /// answer other than a parent that did not resolve is this step's, including
    /// the one that resolves.
    /// </summary>
    Ordinal,
}
