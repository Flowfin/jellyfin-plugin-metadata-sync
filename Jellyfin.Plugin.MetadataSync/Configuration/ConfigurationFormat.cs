namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// The shape the plugin configuration is written in, and which of those shapes
/// this build can read.
/// </summary>
/// <remarks>
/// This is the configuration's half of what <see cref="Store.StoreFormat"/> does
/// for the store directory, and the two are deliberately separate numbers. They
/// stamp two artefacts that survive an upgrade independently: a configuration
/// restored from a backup beside a store that was not, or the other way round,
/// is a state an operator can reach without doing anything unusual, and one
/// number covering both would say the wrong thing about whichever of the two
/// moved.
/// <para>
/// The stamp is read and never written here. The server owns the write: it
/// deserialises this file into whatever the current type is and writes it back
/// when an operator saves, and the only hook a plugin has for that save is on
/// its own entry point, which is built with no way to a service. That is argued
/// at #49 rather than re-argued here. So what this build does with a stamp is
/// place it or refuse it, and what sets it to a later number is the migration
/// step that changes the shape, which is the half of #59 that does not exist.
/// </para>
/// <para>
/// <see cref="Absent"/> is what the serialiser leaves when the element is not in
/// the file, and it is the state every configuration in existence is in today. A
/// plugin that has never been configured and a file written before the stamp
/// existed both hold a configuration of format <see cref="Earliest"/>, and both
/// arrive here as <see cref="Absent"/>, so reading them the same way loses
/// nothing. Every later format is stamped, so an absent stamp cannot mean
/// anything else.
/// </para>
/// <para>
/// A number below <see cref="Absent"/> is refused rather than read as the
/// earliest format. It cannot arrive from the serialiser filling in a default,
/// so something typed it, and the assumption that fails in the destroying
/// direction is the generous one: a file whose stamp was damaged, read as the
/// earliest shape, is acted on under rules it was not written under and saved
/// back that way.
/// </para>
/// </remarks>
public static class ConfigurationFormat
{
    /// <summary>
    /// The value a configuration carrying no stamp arrives with, which is what
    /// the server's serialiser leaves for an element that is not in the file.
    /// </summary>
    public const int Absent = 0;

    /// <summary>
    /// The format a configuration carrying no stamp is read as.
    /// </summary>
    /// <remarks>
    /// It is the same number as <see cref="Current"/> because one format has
    /// existed so far, and it is named separately because the two answer
    /// different questions and stop being the same number at the first
    /// migration. A reader who takes them for one constant will treat every
    /// unstamped configuration as current on the day they differ, which is the
    /// migration silently skipped.
    /// </remarks>
    public const int Earliest = 1;

    /// <summary>
    /// The format this build reads.
    /// </summary>
    public const int Current = 1;

    /// <summary>
    /// The format a stamp means, or nothing where this build cannot place it.
    /// </summary>
    /// <param name="stamp">The number the configuration carries.</param>
    /// <returns>
    /// The format, which is <see cref="Earliest"/> for <see cref="Absent"/>, or
    /// <see langword="null"/> for a stamp naming a shape newer than this build
    /// reads or a number no format has ever had.
    /// </returns>
    public static int? Declared(int stamp)
    {
        if (stamp == Absent)
        {
            return Earliest;
        }

        if (stamp < Earliest || stamp > Current)
        {
            return null;
        }

        return stamp;
    }
}
