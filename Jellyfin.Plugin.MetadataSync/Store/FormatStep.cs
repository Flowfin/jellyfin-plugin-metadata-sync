using System;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// One step of the store's migration chain: the format it starts from, the
/// format it leaves the directory in, and the change itself.
/// </summary>
/// <remarks>
/// A step moves a directory by exactly one format, so <see cref="To"/> is
/// derived rather than given. A chain assembled out of these is contiguous by
/// construction, which is the half of "one step per released version" that
/// cannot then be got wrong: a step declaring both ends could name a pair that
/// skips a format, and the skipped one is the shape nobody wrote a step for.
/// <para>
/// The step is written against the shape its starting number had on the day
/// that number shipped, which is what the section of `docs/RELEASING.md` on a
/// shipped version keeping its meaning binds. Redefining a released number
/// later leaves this step in place and makes it wrong on every installation
/// still holding an artefact written under the old meaning.
/// </para>
/// <para>
/// What it is handed is a working copy of the store directory and never the
/// store itself. Nothing a step does reaches the directory the plugin reads
/// until every step in the chain has finished, so a step that throws costs the
/// copy and leaves the original exactly as the build that wrote it left it.
/// That property is <see cref="StoreFormat.Migrate()"/>'s to keep, and it is
/// stated here because it is the reason a step may be written as a plain
/// transformation with no undo of its own.
/// </para>
/// </remarks>
public sealed class FormatStep
{
    private readonly Action<string> _apply;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatStep"/> class.
    /// </summary>
    /// <param name="from">The format a directory is in before this step runs.</param>
    /// <param name="apply">The change, over the directory it is handed.</param>
    /// <exception cref="ArgumentNullException">There is no change to apply.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The step starts before the earliest format that has existed.</exception>
    public FormatStep(int from, Action<string> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentOutOfRangeException.ThrowIfLessThan(from, StoreFormat.Earliest);

        From = from;
        _apply = apply;
    }

    /// <summary>
    /// Gets the format a directory is in before this step runs.
    /// </summary>
    public int From { get; }

    /// <summary>
    /// Gets the format the directory is in after it.
    /// </summary>
    public int To => From + 1;

    /// <summary>
    /// Runs the change over a directory.
    /// </summary>
    /// <param name="directory">The working copy of the store directory.</param>
    public void Apply(string directory) => _apply(directory);
}
