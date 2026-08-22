using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// A library with more items in it than a test wants to build one at a time:
/// it knows how many it holds and makes each one at the moment it is asked for.
/// </summary>
/// <remarks>
/// Separate from <see cref="LibraryItems"/> rather than a mode on it, because
/// the two are asked different questions. That one is put together item by item
/// and is the arrangement for what a read is about; this one exists so a read
/// can be watched at a size no test would assemble by hand, and it can answer
/// nothing about which item is which beyond its identifier.
/// <para>
/// Making an item per ask is the whole point and not an optimisation. A bound
/// on a read is a statement about how many items are on this side of the call
/// at once, and a proxy holding every item it might be asked for has already
/// paid the cost the bound exists to avoid: the measurement would be of the
/// arrangement rather than of the plugin. What this holds is one identifier per
/// item, which is the same thing a bounded reader holds and is the cost that is
/// stated rather than avoided.
/// </para>
/// <para>
/// It answers a query the way the server answers one. A query naming no
/// identifier is a query about everything these libraries hold, and that is
/// what comes back - every item, made. So a reader that stopped asking for one
/// page at a time is handed the library and is caught by the count, rather than
/// being quietly served an answer the size it should have asked for.
/// </para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1852:Seal internal types",
    Justification = "DispatchProxy.Create generates a subtype of this class at run time, in a dynamic assembly the analyzer cannot see. Sealing it fails the run rather than the build.")]
internal class LibraryOfManyItems : DispatchProxy
{
    /// <summary>
    /// Gets or sets the library everything here is in.
    /// </summary>
    public Guid Library { get; set; }

    /// <summary>
    /// Gets or sets how many items this library holds.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets every member that was called, in the order it was called.
    /// </summary>
    public Collection<string> Called { get; } = new();

    /// <summary>
    /// Gets how many identifiers each query named, one entry per query.
    /// </summary>
    public Collection<int> AskedForItems { get; } = new();

    /// <summary>
    /// Gets the libraries each query named as ancestors, one entry per query.
    /// </summary>
    public Collection<IReadOnlyList<Guid>> AskedFor { get; } = new();

    /// <summary>
    /// Gets how many items this library has made and handed over, across every
    /// call.
    /// </summary>
    public int Handed { get; private set; }

    /// <summary>
    /// Builds a library holding a stated number of items.
    /// </summary>
    /// <param name="library">The library they are all in.</param>
    /// <param name="total">How many there are.</param>
    /// <returns>The library the plugin sees, and the record of what it did.</returns>
    public static (ILibraryManager Library, LibraryOfManyItems Items) Holding(Guid library, int total)
    {
        var proxy = DispatchProxy.Create<ILibraryManager, LibraryOfManyItems>();
        var items = (LibraryOfManyItems)proxy;

        items.Library = library;
        items.Total = total;

        return (proxy, items);
    }

    /// <summary>
    /// The identifier the item at a position is held under, derived from the
    /// position so a test can say which items it expected without holding
    /// them.
    /// </summary>
    /// <param name="position">Which item, counted from zero.</param>
    /// <returns>Its identifier.</returns>
    public static Guid IdentifierAt(int position)
    {
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), position);
        return new Guid(bytes);
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        Called.Add(targetMethod.Name);

        var query = (InternalItemsQuery)args![0]!;
        var ancestors = query.AncestorIds ?? Array.Empty<Guid>();
        var wanted = query.ItemIds ?? Array.Empty<Guid>();

        AskedForItems.Add(wanted.Length);
        AskedFor.Add(ancestors.ToList());

        var positions = Positions(ancestors, wanted);

        if (IsGetItemIds(targetMethod))
        {
            return positions.Select(IdentifierAt).ToList();
        }

        if (IsGetItemList(targetMethod))
        {
            var made = positions.Select(Made).ToList();
            Handed += made.Count;
            return made;
        }

        throw new NotSupportedException(string.Format(
            CultureInfo.InvariantCulture,
            "Reading the items of a pass called ILibraryManager.{0}, and the only members it may call are GetItemIds and GetItemList.",
            targetMethod.Name));
    }

    private static bool IsGetItemList(MethodInfo method)
    {
        return string.Equals(method.Name, nameof(ILibraryManager.GetItemList), StringComparison.Ordinal)
            && method.GetParameters().Length == 1;
    }

    private static bool IsGetItemIds(MethodInfo method)
    {
        return string.Equals(method.Name, nameof(ILibraryManager.GetItemIds), StringComparison.Ordinal)
            && method.GetParameters().Length == 1;
    }

    private static BaseItem Made(int position)
    {
        return new Movie
        {
            Id = IdentifierAt(position),
            Overview = string.Format(CultureInfo.InvariantCulture, "item {0}", position)
        };
    }

    /// <summary>
    /// Which item an identifier names, which is the position
    /// <see cref="IdentifierAt"/> wrote into it.
    /// </summary>
    /// <remarks>
    /// Read back rather than searched for. A proxy that answered a page by
    /// walking everything it holds and keeping the identifiers it was asked
    /// about would do that walk once per page, which at the size this exists
    /// for is the arrangement costing more than the thing being measured.
    /// </remarks>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The position, which is outside the range if it names no item.</returns>
    private static int PositionOf(Guid identifier)
    {
        Span<byte> bytes = stackalloc byte[16];
        identifier.TryWriteBytes(bytes);
        return BitConverter.ToInt32(bytes[..4]);
    }

    private IEnumerable<int> Positions(IReadOnlyList<Guid> ancestors, IReadOnlyList<Guid> wanted)
    {
        if (ancestors.Count > 0 && !ancestors.Contains(Library))
        {
            return Array.Empty<int>();
        }

        if (wanted.Count == 0)
        {
            // Everything, which is what a query naming no identifier means.
            return Enumerable.Range(0, Total);
        }

        return wanted
            .Select(PositionOf)
            .Where(position => position >= 0 && position < Total);
    }
}
