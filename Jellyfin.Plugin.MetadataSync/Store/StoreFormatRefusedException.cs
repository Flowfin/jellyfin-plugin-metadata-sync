using System;
using System.Globalization;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// Thrown when the store directory declares a format this build does not read.
/// </summary>
/// <remarks>
/// Both spellings of that are a downgrade rather than a mistake an operator
/// made. A directory stamped with a format from the future was written by a
/// newer build of this plugin, and a stamp that cannot be read is a stamp this
/// build cannot place at all. Neither is opened, because reading a newer file
/// half-successfully is how a downgrade destroys data: the members a newer
/// format added are dropped in silence by the reader, and the next compaction
/// writes the file back without them.
/// <para>
/// Refusing is the fail-closed direction and it costs a plugin that will not
/// start until the operator puts the newer build back or moves the directory
/// aside. That is the trade this exception exists to make, and the alternative
/// is a plugin that starts and quietly discards what the newer build recorded.
/// </para>
/// <para>
/// One constructor, and the three an exception usually carries are absent on
/// purpose, for the reason written at
/// <see cref="StoreRemovedADifferentNumberException"/>: `CA1032` is set to
/// `Info` in `jellyfin.ruleset` and the coverage bar reads a constructor
/// nothing calls as a decision nothing exercises.
/// </para>
/// </remarks>
public sealed class StoreFormatRefusedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreFormatRefusedException"/> class
    /// naming the stamp, what it says and what this build writes.
    /// </summary>
    /// <param name="path">The stamp file the format was read from.</param>
    /// <param name="found">What the stamp says, in the words it was read in.</param>
    /// <param name="current">The format this build reads and writes.</param>
    public StoreFormatRefusedException(string path, string found, int current)
        : base(string.Format(
            CultureInfo.InvariantCulture,
            "{0} says {1}. This plugin reads and writes store format {2} and will not open a store it cannot place, so nothing in this directory has been read.",
            path,
            found,
            current))
    {
    }
}
