using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The server's library, as far as reading which libraries it holds is
/// concerned: one member that answers and every other one that refuses.
/// </summary>
/// <remarks>
/// A second proxy beside <see cref="LibraryCalls"/> rather than a third
/// answered member on it. What each of the two proves is the set of members the
/// path under test may call, and a member added to one proxy for the other
/// path's sake widens both. The write path may call two things and this path
/// may call one, and neither statement survives being merged with the other.
/// <para>
/// It is a dispatch proxy for the reason that one is: an implementation written
/// out by hand answers every member with a default, so a path reaching for an
/// item query would be served quietly and the test would pass.
/// </para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1852:Seal internal types",
    Justification = "DispatchProxy.Create generates a subtype of this class at run time, in a dynamic assembly the analyzer cannot see. Sealing it fails the run rather than the build.")]
internal class LibraryFolders : DispatchProxy
{
    /// <summary>
    /// Gets the libraries this server lists, as the server describes them.
    /// </summary>
    public List<VirtualFolderInfo> Folders { get; } = new();

    /// <summary>
    /// Gets every member that was called, in the order it was called.
    /// </summary>
    public List<string> Called { get; } = new();

    /// <summary>
    /// Builds a library listing the identifiers given, and the handle onto what
    /// it was asked.
    /// </summary>
    /// <param name="ids">The libraries the server holds.</param>
    /// <returns>The library the plugin sees, and the record of what it did.</returns>
    public static (ILibraryManager Library, LibraryFolders Folders) Listing(params Guid[] ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var library = DispatchProxy.Create<ILibraryManager, LibraryFolders>();
        var folders = (LibraryFolders)library;

        foreach (var id in ids)
        {
            folders.Folders.Add(new VirtualFolderInfo
            {
                Name = id.ToString("N", CultureInfo.InvariantCulture),
                ItemId = id.ToString("N", CultureInfo.InvariantCulture)
            });
        }

        return (library, folders);
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        Called.Add(targetMethod.Name);

        if (string.Equals(targetMethod.Name, nameof(ILibraryManager.GetVirtualFolders), StringComparison.Ordinal)
            && targetMethod.GetParameters().Length == 0)
        {
            return Folders;
        }

        throw new NotSupportedException(string.Format(
            CultureInfo.InvariantCulture,
            "Reading which libraries the server holds called ILibraryManager.{0}, and the only member it may call is GetVirtualFolders.",
            targetMethod.Name));
    }
}
