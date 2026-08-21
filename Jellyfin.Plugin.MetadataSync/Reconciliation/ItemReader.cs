using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// The items a pass may look at: the ones held by the libraries the operator
/// chose, and no others.
/// </summary>
/// <remarks>
/// The set of participating libraries is taken at construction and never read
/// again, so one pass reads one answer. An operator who changes the selection
/// while a pass is running changes the next pass and not this one, which is the
/// weaker of the two properties here and the easier one to lose: a reader that
/// asked the configuration per batch would enumerate under one selection and
/// finish under another, and nothing in the result would say which.
/// <para>
/// The stronger property is that a library that does not take part is never
/// named in a query rather than dropped from an answer. The difference is what
/// #42 is about. A reader that asked the server for everything and kept the
/// items whose ancestor is in the set gives the same answer today and reaches a
/// library an operator excluded the day the keeping is wrong; this one cannot
/// reach it, because the identifiers that go into the query are the set itself.
/// </para>
/// <para>
/// An empty set is the case worth being deliberate about, and it is the default
/// a plugin arrives installed with. Empty means no library takes part, never
/// all of them, so this asks the server nothing at all and answers with nothing.
/// The trap it is avoiding is in the query type rather than here: a recursive
/// query carrying no ancestor is a query over the whole server, so an empty set
/// turned into a query is the one mistake that reads as harmless and enumerates
/// everything.
/// </para>
/// <para>
/// What it does not do. It does not ask whether an identifier is a library this
/// server holds - <c>ConfigurationValidation</c> refuses a configuration naming
/// one the server does not have, and a second answer here would be a second
/// place for that to be decided. It does not bound how many items come back in
/// one call, which is #37 and is the reason this returns what the server hands
/// it rather than promising a shape. And it holds nothing between passes: the
/// next pass reads again, because the library moved while nothing was running,
/// which is the property #38 needs from a read.
/// </para>
/// </remarks>
public sealed class ItemReader
{
    private readonly ILibraryManager _library;
    private readonly Guid[] _participating;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemReader"/> class.
    /// </summary>
    /// <param name="library">The server's library.</param>
    /// <param name="participating">The libraries that take part in this pass.</param>
    public ItemReader(ILibraryManager library, IEnumerable<Guid> participating)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(participating);

        _library = library;

        // Copied rather than held. What arrives is the collection on the
        // configuration type, which the server rewrites when an operator saves
        // the page, so a reader keeping the reference would change set under
        // its own pass.
        _participating = participating.ToArray();
    }

    /// <summary>
    /// Reads the items the participating libraries hold.
    /// </summary>
    /// <returns>The items, as the server hands them over.</returns>
    public IReadOnlyList<BaseItem> Read()
    {
        if (_participating.Length == 0)
        {
            // No query. Returning an empty list after asking would be the same
            // answer for this server and a different act: the ask is the thing
            // that reaches a library, and there is no library this pass may
            // reach.
            return Array.Empty<BaseItem>();
        }

        return _library.GetItemList(new InternalItemsQuery
        {
            // The set is the bound. Everything under one of these libraries is
            // in scope and nothing else is expressible, which is why the
            // participating libraries are the ancestors rather than a filter
            // applied to what comes back.
            AncestorIds = _participating.ToArray(),

            // Without this the answer is the libraries' immediate children,
            // which for a television library is a list of series and no
            // episodes.
            Recursive = true
        });
    }
}
