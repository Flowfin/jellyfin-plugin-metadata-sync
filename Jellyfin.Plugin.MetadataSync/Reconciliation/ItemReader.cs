using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MetadataSync.Reconciliation;

/// <summary>
/// The items a pass may look at: the ones held by the libraries the operator
/// chose, and no others, handed over a page at a time.
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
/// items whose library is in the set gives the same answer today and reaches a
/// library an operator excluded the day the keeping is wrong; this one cannot
/// reach it, because the identifiers that go into the query are the set itself.
/// Every query this reader makes carries them, the page queries included, so
/// the property is one a reader of any single call can check rather than one
/// that holds only if an earlier call happened to be right.
/// </para>
/// <para>
/// An empty set is the case worth being deliberate about, and it is the default
/// a plugin arrives installed with. Empty means no library takes part, never
/// all of them, so this asks the server nothing at all and answers with nothing.
/// The trap it is avoiding is in the query type rather than here: a recursive
/// query carrying no ancestor is a query over everything the server holds, so
/// an empty set turned into a query is the one mistake that reads as harmless
/// and enumerates everything.
/// </para>
/// <para>
/// What is in memory at once is <see cref="PageSize"/> items and the
/// identifiers of everything that takes part, which is the bound #37 asks of a
/// read. The identifiers are asked for first and in one call, so the pass works
/// from one answer to one question rather than from a walk that can be
/// overtaken: paging by offset over a library something else is writing to
/// skips an item when an earlier one is deleted underneath, and the skipped
/// item never reaches a plan, so the pass reports a success it did not carry
/// out. A list of identifiers cannot be overtaken that way. It costs sixteen
/// bytes per participating item, which is one and a half megabytes at the
/// hundred thousand items the suite reads, against the whole of those items'
/// metadata if the items themselves were held instead.
/// </para>
/// <para>
/// What that does not promise. An item deleted after the identifiers were read
/// is not handed back by the server and is absent from its page, and an item
/// created after them is not seen until the next pass. Both are the library
/// moving under a pass rather than a defect, and neither loses an item silently
/// the way an offset walk does: the identifier was read once and what became of
/// it is answerable. Nothing is locked across a page either, so an item can
/// still change between the page it arrived on and the moment a write is
/// attempted, which is the window <c>docs/reconciliation.md</c> describes and
/// the write path defends rather than this one.
/// </para>
/// <para>
/// What it does not do. It does not ask whether an identifier is a library this
/// server holds - <c>ConfigurationValidation</c> refuses a configuration naming
/// one the server does not have, and a second answer here would be a second
/// place for that to be decided. The page size is a constant here rather than a
/// setting: #37 asks for it in configuration with a maximum a configuration
/// cannot exceed, alongside three other bounds, and two of those four have no
/// number anybody can defend yet. And it holds nothing between passes: the next
/// pass reads again, because the library moved while nothing was running, which
/// is the property #38 needs from a read.
/// </para>
/// </remarks>
public sealed class ItemReader
{
    /// <summary>
    /// How many items are fetched from the server in one call.
    /// </summary>
    /// <remarks>
    /// The suite reads this rather than restating it, so an assertion is about
    /// this number and not about a copy that stops moving when this one does.
    /// The value is chosen rather than measured: big enough that a large
    /// library is not a hundred thousand round trips, small enough that one
    /// page is not itself worth bounding. #37 is where a measured one would
    /// replace it.
    /// </remarks>
    internal const int PageSize = 500;

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
    /// Reads the items the participating libraries hold, a page at a time.
    /// </summary>
    /// <returns>
    /// The items, as the server hands them over. Nothing is asked of the server
    /// until the answer is enumerated, and no more than <see cref="PageSize"/>
    /// items are asked for at once.
    /// </returns>
    public IEnumerable<BaseItem> Read()
    {
        if (_participating.Length == 0)
        {
            // No query. Answering with nothing after asking would be the same
            // answer for this server and a different act: the ask is the thing
            // that reaches a library, and there is no library this pass may
            // reach.
            yield break;
        }

        var identifiers = _library.GetItemIds(Under(Array.Empty<Guid>()));

        for (var taken = 0; taken < identifiers.Count; taken += PageSize)
        {
            var page = new Guid[Math.Min(PageSize, identifiers.Count - taken)];

            for (var offset = 0; offset < page.Length; offset++)
            {
                page[offset] = identifiers[taken + offset];
            }

            foreach (var item in _library.GetItemList(Under(page)))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// A query under the participating libraries, narrowed to one page of
    /// identifiers where there is one.
    /// </summary>
    /// <param name="page">
    /// The identifiers this query is about, or an empty set for a query about
    /// everything the participating libraries hold.
    /// </param>
    /// <returns>The query.</returns>
    private InternalItemsQuery Under(Guid[] page)
    {
        return new InternalItemsQuery
        {
            // The set is the bound. Everything under one of these libraries is
            // in scope and nothing else is expressible, which is why the
            // participating libraries are the ancestors rather than a filter
            // applied to what comes back. A page names them as well: an
            // identifier read a moment ago is not a licence to fetch an item
            // without saying which libraries this pass may reach.
            AncestorIds = _participating.ToArray(),

            // Without this the answer is the libraries' immediate children,
            // which for a television library is a list of series and no
            // episodes.
            Recursive = true,

            ItemIds = page
        };
    }
}
