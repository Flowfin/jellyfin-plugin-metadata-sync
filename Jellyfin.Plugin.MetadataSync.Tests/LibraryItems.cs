using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The server's library, as far as reading items out of it is concerned: one
/// member that answers and every other one that refuses.
/// </summary>
/// <remarks>
/// A third proxy beside <see cref="LibraryCalls"/> and
/// <see cref="LibraryFolders"/> rather than another answered member on either,
/// for the reason <see cref="LibraryFolders"/> already gives: what each proxy
/// proves is the set of members its path may call, and a member added to one
/// for another path's sake widens both. The write path may call two things,
/// the listing path may call one, and this path may call one different one.
/// <para>
/// What separates it from a stub is that it answers a query the way the server
/// answers one, rather than the way a reader would like it answered. A
/// recursive query carrying no ancestor is a query over everything the server
/// holds, and that is what this hands back. So a reader that asked for
/// everything and narrowed the answer afterwards is caught by the items it
/// returns, which is an observation, rather than by a rule this file invented
/// about what a query should look like.
/// </para>
/// <para>
/// It is not a server. There is no hierarchy in it: an item sits in exactly one
/// library and nothing here has a parent, so a query naming a library answers
/// with what was put in that library and nothing is walked. That is enough for
/// what <c>ParticipatingLibraryTests</c> asks and it is not enough for anything
/// about ordering, paging or the shape of a real result.
/// </para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1852:Seal internal types",
    Justification = "DispatchProxy.Create generates a subtype of this class at run time, in a dynamic assembly the analyzer cannot see. Sealing it fails the run rather than the build.")]
internal class LibraryItems : DispatchProxy
{
    /// <summary>
    /// Gets the items this server holds, by the library each one is in.
    /// </summary>
    public Dictionary<Guid, List<BaseItem>> ByLibrary { get; } = new();

    /// <summary>
    /// Gets every member that was called, in the order it was called, so a
    /// call that was never made is as visible as one that was.
    /// </summary>
    public Collection<string> Called { get; } = new();

    /// <summary>
    /// Gets the libraries each query named as ancestors, one entry per query.
    /// A query naming none is recorded as an empty entry rather than dropped.
    /// </summary>
    public Collection<IReadOnlyList<Guid>> AskedFor { get; } = new();

    /// <summary>
    /// Builds a library with nothing in it, and the handle onto what it was
    /// asked.
    /// </summary>
    /// <returns>The library the plugin sees, and the record of what it did.</returns>
    public static (ILibraryManager Library, LibraryItems Items) Empty()
    {
        var library = DispatchProxy.Create<ILibraryManager, LibraryItems>();
        return (library, (LibraryItems)library);
    }

    /// <summary>
    /// Puts an item in a library, taking it out of whichever one it was in.
    /// </summary>
    /// <param name="library">The library it is in from now on.</param>
    /// <param name="item">The item.</param>
    public void Put(Guid library, BaseItem item)
    {
        foreach (var held in ByLibrary.Values)
        {
            held.Remove(item);
        }

        if (!ByLibrary.TryGetValue(library, out var items))
        {
            items = new List<BaseItem>();
            ByLibrary[library] = items;
        }

        items.Add(item);
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        Called.Add(targetMethod.Name);

        if (IsGetItemList(targetMethod))
        {
            var query = (InternalItemsQuery)args![0]!;
            var ancestors = query.AncestorIds ?? Array.Empty<Guid>();

            AskedFor.Add(ancestors.ToList());

            // No ancestor named is not "nothing". It is the whole server, which
            // is what the query means and why an empty participating set turned
            // into a query would read everything an operator excluded.
            var libraries = ancestors.Length == 0 ? ByLibrary.Keys.ToList() : ancestors.ToList();

            return libraries
                .Where(ByLibrary.ContainsKey)
                .SelectMany(library => ByLibrary[library])
                .ToList();
        }

        throw new NotSupportedException(string.Format(
            CultureInfo.InvariantCulture,
            "Reading the items of a pass called ILibraryManager.{0}, and the only member it may call is GetItemList.",
            targetMethod.Name));
    }

    private static bool IsGetItemList(MethodInfo method)
    {
        return string.Equals(method.Name, nameof(ILibraryManager.GetItemList), StringComparison.Ordinal)
            && method.GetParameters().Length == 1;
    }
}
