using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The server's library, as far as the write path is concerned: two members
/// that answer and seventy-five that refuse.
/// </summary>
/// <remarks>
/// It is a dispatch proxy rather than a class implementing the interface, and
/// the reason is the property rather than the typing. An implementation written
/// out by hand answers every member with a default, so a write path that reached
/// for the item repository, the user manager or a query would be served quietly
/// and the test would pass. Here every member except the two the write path is
/// allowed to use throws and names itself, so "this path calls nothing else" is
/// something the suite observes rather than something a reader checks by eye.
/// <para>
/// It also survives the interface moving. #9 carries two server lines with
/// different surfaces, and a hand-written implementation is a file that has to
/// be edited on the day the second target lands, in a way that consists of
/// adding members nobody reads.
/// </para>
/// <para>
/// What it is not is a server. It records what it was asked for and holds items
/// in a dictionary; nothing here saves anything, raises an event or runs a
/// metadata saver, so a test using it proves what this plugin asked the server
/// to do and never what the server then did. <c>docs/reconciliation.md</c> is
/// where the second half is argued, out of the server's own source.
/// </para>
/// </remarks>
// Not sealed, and not abstract either, because the dispatch proxy derives from
// this type at run time and refuses both. The analyzer reads the assembly and
// sees no subtype, which is true of the assembly and false of the run.
[SuppressMessage(
    "Performance",
    "CA1852:Seal internal types",
    Justification = "DispatchProxy.Create generates a subtype of this class at run time, in a dynamic assembly the analyzer cannot see. Sealing it fails the run rather than the build.")]
internal class LibraryCalls : DispatchProxy
{
    /// <summary>
    /// Gets the items this library holds, by identifier.
    /// </summary>
    public Dictionary<Guid, BaseItem> Items { get; } = new();

    /// <summary>
    /// Gets every member that was called, in the order it was called, so a call
    /// that was never made is as visible as one that was.
    /// </summary>
    public Collection<string> Called { get; } = new();

    /// <summary>
    /// Gets the update calls, with everything each one carried.
    /// </summary>
    public Collection<Update> Updates { get; } = new();

    /// <summary>
    /// Builds a library with nothing in it, and the handle onto what it was
    /// asked.
    /// </summary>
    /// <returns>The library the plugin sees, and the record of what it did.</returns>
    public static (ILibraryManager Library, LibraryCalls Calls) Empty()
    {
        var library = DispatchProxy.Create<ILibraryManager, LibraryCalls>();
        return (library, (LibraryCalls)library);
    }

    /// <summary>
    /// Builds a library holding one item.
    /// </summary>
    /// <param name="id">The identifier the item answers to.</param>
    /// <param name="item">The item.</param>
    /// <returns>The library the plugin sees, and the record of what it did.</returns>
    public static (ILibraryManager Library, LibraryCalls Calls) Holding(Guid id, BaseItem item)
    {
        var (library, calls) = Empty();
        calls.Items[id] = item;
        return (library, calls);
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        Called.Add(targetMethod.Name);

        if (IsGetItemById(targetMethod))
        {
            var id = (Guid)args![0]!;
            return Items.TryGetValue(id, out var found) ? found : null;
        }

        if (IsUpdateItemAsync(targetMethod))
        {
            Updates.Add(new Update(
                (BaseItem)args![0]!,
                (BaseItem?)args[1],
                (ItemUpdateType)args[2]!,
                (CancellationToken)args[3]!));

            return Task.CompletedTask;
        }

        throw new NotSupportedException(string.Format(
            CultureInfo.InvariantCulture,
            "The write path called ILibraryManager.{0}, and the only members it may call are GetItemById and UpdateItemAsync.",
            targetMethod.Name));
    }

    private static bool IsGetItemById(MethodInfo method)
    {
        return string.Equals(method.Name, nameof(ILibraryManager.GetItemById), StringComparison.Ordinal)
            && !method.IsGenericMethod
            && method.GetParameters().Length == 1;
    }

    private static bool IsUpdateItemAsync(MethodInfo method)
    {
        return string.Equals(method.Name, nameof(ILibraryManager.UpdateItemAsync), StringComparison.Ordinal);
    }

    /// <summary>
    /// One call to the supported update, with every argument it carried.
    /// </summary>
    /// <param name="Item">The item that was handed over.</param>
    /// <param name="Parent">The parent that travelled with it.</param>
    /// <param name="Reason">The update reason.</param>
    /// <param name="Token">The token the call was made under.</param>
    internal sealed record Update(BaseItem Item, BaseItem? Parent, ItemUpdateType Reason, CancellationToken Token);
}
