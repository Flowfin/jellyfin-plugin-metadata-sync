using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// Answers which libraries this server holds, which is the range a
/// participating library in the configuration is checked against.
/// </summary>
/// <remarks>
/// The libraries an operator can choose between are the ones the server's own
/// library administration lists, so that is what this reads. Enumerating items
/// and taking their top-most parent would answer a different question and would
/// walk the library to do it, which is the thing #37 exists to bound.
/// <para>
/// An entry whose identifier is not a value this plugin can hold is left out
/// rather than reported. What this answers is a range, and a range that
/// contains something unreadable is a range nothing can be checked against;
/// the effect on an operator is that a library the server describes with no
/// usable identifier reads as one the server does not hold, and the validator
/// then names it. That is the fail-closed direction and it is stated here
/// because it is invisible at the call site.
/// </para>
/// </remarks>
internal static class ServerLibraries
{
    /// <summary>
    /// Reads the libraries this server holds.
    /// </summary>
    /// <param name="library">The server's library.</param>
    /// <returns>The identifier of every library the server lists.</returns>
    public static IReadOnlyCollection<Guid> Held(ILibraryManager library)
    {
        ArgumentNullException.ThrowIfNull(library);

        var held = new List<Guid>();

        foreach (var folder in library.GetVirtualFolders())
        {
            if (Guid.TryParse(folder.ItemId, out var id))
            {
                held.Add(id);
            }
        }

        return held;
    }
}
