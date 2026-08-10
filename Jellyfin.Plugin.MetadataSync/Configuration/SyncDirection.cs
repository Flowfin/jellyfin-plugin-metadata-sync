namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// Which way metadata moves for a pairing.
/// </summary>
/// <remarks>
/// One member, and that is the answer rather than a start. The direction
/// belongs to the pairing, both servers pull from each other, and no field
/// carries a direction of its own. A second member here would be a second
/// conflict model, so adding one is a decision somebody argues for and not a
/// line appended to an enum.
/// <para>
/// It is an enum with one member rather than nothing at all because a
/// configuration read off disk can carry a number this plugin has never
/// declared, and the validator has to be able to say so by name. A property
/// that cannot hold a wrong value also cannot report one.
/// </para>
/// </remarks>
public enum SyncDirection
{
    /// <summary>
    /// Both servers pull from each other under one rule set.
    /// </summary>
    TwoWay = 0,
}
