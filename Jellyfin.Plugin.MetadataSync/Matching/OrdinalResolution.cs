namespace Jellyfin.Plugin.MetadataSync.Matching;

/// <summary>
/// What one parent-plus-ordinal resolution established, which step established
/// it, and the sentence a register entry is written from.
/// </summary>
/// <remarks>
/// The step is carried rather than derived by whoever reads this, because the
/// reason for carrying it is that two failures look alike in a list and are
/// different work to fix.
/// </remarks>
public sealed class OrdinalResolution
{
    internal OrdinalResolution(OrdinalVerdict verdict, OrdinalStep step, OrdinalIdentity? match, string reason)
    {
        Verdict = verdict;
        Step = step;
        Match = match;
        Reason = reason;
    }

    /// <summary>
    /// Gets what was established.
    /// </summary>
    public OrdinalVerdict Verdict { get; }

    /// <summary>
    /// Gets the step that established it. Everything except a parent that did
    /// not resolve is the ordinal step's, including a resolution.
    /// </summary>
    public OrdinalStep Step { get; }

    /// <summary>
    /// Gets the item this one is the same as, which is set only where the
    /// verdict is <see cref="OrdinalVerdict.Resolved"/> and is null otherwise.
    /// </summary>
    public OrdinalIdentity? Match { get; }

    /// <summary>
    /// Gets the sentence this outcome is reported by, naming the numbers it was
    /// decided from so a register entry says what was compared rather than only
    /// that it failed.
    /// </summary>
    public string Reason { get; }
}
