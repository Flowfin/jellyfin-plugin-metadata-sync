using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// How much of a library a pass holds while it reads one, which is the bound
/// #37 asks of the read.
/// </summary>
/// <remarks>
/// The failure this is about does not look like a failure. A read that asks the
/// server once and is handed a list answers correctly on every library a test
/// builds by hand, and on a library of fifty thousand items on a server that is
/// also transcoding it is the first thing a naive pass does wrong. So what is
/// asserted here is not the answer, which was already right, but how many items
/// are on this side of the call at once while the answer is being produced.
/// <para>
/// That is watched rather than measured. A byte count taken around an
/// enumeration is dominated by the items the arrangement makes, so it would be
/// a measurement of the substitute, and the number it produced would move with
/// the runtime rather than with this plugin. What the legs below observe is the
/// count of items the library has handed over and the count the caller has
/// consumed, and the difference between the two is exactly what a bound on a
/// read is about. A reader that materialised would show the whole library on
/// the first of those numbers and one item on the second.
/// </para>
/// <para>
/// The bound is stated in items rather than in bytes for the same reason, and
/// what it leaves out is stated on <see cref="ItemReader"/>: the identifiers of
/// everything that takes part are held for the length of the pass, which is
/// sixteen bytes each, and that is a cost taken on purpose against paging by
/// offset over a library something else is writing to.
/// </para>
/// </remarks>
public class BoundedReadTests
{
    private static readonly Guid _shared = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _private = new("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// A library of a size no test assembles by hand. It is the number #37
    /// names, and it is here so the legs about a bound run against something
    /// that would hurt if the bound were not held.
    /// </summary>
    private const int ManyItems = 100_000;

    /// <summary>
    /// The page these legs read at. It is the configuration's own default,
    /// read rather than restated, so an assertion here is about the number the
    /// plugin ships with and not about a copy that stops moving when that one
    /// does.
    /// </summary>
    private const int PageSize = PluginConfiguration.ItemsPerReadDefault;

    /// <summary>
    /// Which items there are is one question and asked once; which items those
    /// are is asked a page at a time afterwards. Asking the second question
    /// first is the shape that cannot be bounded, because the answer to it is
    /// the library.
    /// </summary>
    [Fact]
    public void TheItemsThereAreAreAskedForBeforeAnyOfThemIs()
    {
        var (library, items) = LibraryOfManyItems.Holding(_shared, PageSize + 1);

        Consume(new ItemReader(library, new[] { _shared }, PageSize).Read());

        Assert.Equal(nameof(ILibraryManager.GetItemIds), items.Called[0]);
        Assert.Equal(1, items.Called.Count(called => string.Equals(called, nameof(ILibraryManager.GetItemIds), StringComparison.Ordinal)));
    }

    /// <summary>
    /// The bound itself, on the ask. No call asks the server for more items
    /// than one page.
    /// </summary>
    [Fact]
    public void NoQueryAsksForMoreThanOnePageOfItems()
    {
        var (library, items) = LibraryOfManyItems.Holding(_shared, (PageSize * 2) + 1);

        Consume(new ItemReader(library, new[] { _shared }, PageSize).Read());

        // The first entry is the identifier query, which names no item and is
        // the one call this bound is not about.
        Assert.All(items.AskedForItems.Skip(1), asked => Assert.InRange(asked, 1, PageSize));
        Assert.Equal(3, items.AskedForItems.Count - 1);
    }

    /// <summary>
    /// The bound as the caller sees it. At no moment has the library handed
    /// over more than one page beyond what has been consumed, over a library
    /// two hundred pages long.
    /// </summary>
    /// <remarks>
    /// This is the leg a materialising read fails, and it fails it by the whole
    /// library rather than by one item, which is the distance between the two
    /// designs.
    /// </remarks>
    [Fact]
    public void NoMoreThanOnePageOfItemsExistsAtOnce()
    {
        var (library, items) = LibraryOfManyItems.Holding(_shared, ManyItems);

        var consumed = 0;

        foreach (var item in new ItemReader(library, new[] { _shared }, PageSize).Read())
        {
            consumed++;

            Assert.InRange(items.Handed - consumed, 0, PageSize);
        }

        Assert.Equal(ManyItems, consumed);
        Assert.Equal(ManyItems, items.Handed);
    }

    /// <summary>
    /// Every item is handed over, once, at a size that is not a whole number of
    /// pages. The last page is the one an off-by-one loses or repeats, and a
    /// library whose size divides evenly cannot tell the difference.
    /// </summary>
    [Fact]
    public void EveryItemIsHandedOverExactlyOnceAcrossThePages()
    {
        var total = (PageSize * 2) + 1;
        var (library, _) = LibraryOfManyItems.Holding(_shared, total);

        var read = new ItemReader(library, new[] { _shared }, PageSize).Read().Select(item => item.Id).ToList();

        Assert.Equal(Enumerable.Range(0, total).Select(LibraryOfManyItems.IdentifierAt), read);
    }

    /// <summary>
    /// Nothing is asked of the server by building an answer, only by reading
    /// one. A read that queried on construction would have taken its page
    /// before whoever asked for it decided to look at any of it.
    /// </summary>
    [Fact]
    public void NothingIsAskedOfTheServerUntilTheAnswerIsRead()
    {
        var (library, items) = LibraryOfManyItems.Holding(_shared, PageSize + 1);

        var answer = new ItemReader(library, new[] { _shared }, PageSize).Read();

        Assert.Empty(items.Called);

        Assert.Equal(PageSize, answer.Take(PageSize).Count());
        Assert.Equal(PageSize, items.Handed);
    }

    /// <summary>
    /// Every query names the participating libraries, the page queries
    /// included. An identifier read a moment ago is not a licence to fetch an
    /// item without saying which libraries this pass may reach.
    /// </summary>
    /// <remarks>
    /// This is #42's property held over a read that now makes several calls
    /// where it made one. A page query naming only identifiers would answer the
    /// same items today and would stop being a query a reader can check on its
    /// own, which is what that property is for.
    /// </remarks>
    [Fact]
    public void EveryQueryNamesTheParticipatingLibrariesAndNoOther()
    {
        var (library, items) = LibraryOfManyItems.Holding(_shared, PageSize + 1);

        Consume(new ItemReader(library, new[] { _shared }, PageSize).Read());

        Assert.NotEmpty(items.AskedFor);
        Assert.All(items.AskedFor, asked => Assert.Equal(new[] { _shared }, asked));
        Assert.DoesNotContain(_private, items.AskedFor.SelectMany(asked => asked));
    }

    /// <summary>
    /// The leg that makes the ones above worth reading. The library answers a
    /// query naming no identifier with everything it holds, exactly as the
    /// query means, so a reader that stopped asking a page at a time is handed
    /// the library and caught by the counts rather than quietly served an
    /// answer the size it should have asked for.
    /// </summary>
    [Fact]
    public void AQueryNamingNoPageIsAnsweredWithEverythingTheLibraryHolds()
    {
        var (library, items) = LibraryOfManyItems.Holding(_shared, (PageSize * 2) + 1);

        var everything = library.GetItemList(new InternalItemsQuery
        {
            AncestorIds = new[] { _shared },
            Recursive = true
        });

        Assert.Equal((PageSize * 2) + 1, everything.Count);
        Assert.Equal(everything.Count, items.Handed);
    }

    /// <summary>
    /// The same, for the identifier query the read opens with: it is answered
    /// with every identifier and not with a page, so the read is what bounds
    /// the pages rather than the arrangement.
    /// </summary>
    [Fact]
    public void TheIdentifierQueryIsAnsweredWithEveryIdentifierTheLibraryHolds()
    {
        var (library, _) = LibraryOfManyItems.Holding(_shared, ManyItems);

        var identifiers = library.GetItemIds(new InternalItemsQuery
        {
            AncestorIds = new[] { _shared },
            Recursive = true
        });

        Assert.Equal(ManyItems, identifiers.Count);
    }

    /// <summary>
    /// The page is the number the caller was given and not one this type holds.
    /// A reader that kept a constant of its own would answer identically at the
    /// default and differ nowhere else, so the assertion is made at a size the
    /// default is not.
    /// </summary>
    [Fact]
    public void ThePagesAreTheSizeTheReaderWasGivenRatherThanTheDefault()
    {
        const int Chosen = 7;

        var (library, items) = LibraryOfManyItems.Holding(_shared, (Chosen * 3) + 1);

        Consume(new ItemReader(library, new[] { _shared }, Chosen).Read());

        Assert.All(items.AskedForItems.Skip(1), asked => Assert.InRange(asked, 1, Chosen));
        Assert.Equal(4, items.AskedForItems.Count - 1);
    }

    /// <summary>
    /// The range the configuration declares is the operator's and not this
    /// type's, so a page above the maximum is read rather than refused here.
    /// </summary>
    /// <remarks>
    /// The direction matters. A second copy of the maximum on this type would
    /// be a second place the range is decided, and the two would answer
    /// differently the day one of them moved. What refuses an operator's number
    /// is <c>ConfigurationValidation</c>, which is where the whole
    /// configuration is judged and where the sentence an operator reads is
    /// written.
    /// </remarks>
    [Fact]
    public void APageAboveWhatAConfigurationMayExpressIsStillARead()
    {
        var above = PluginConfiguration.ItemsPerReadMaximum + 1;
        var (library, items) = LibraryOfManyItems.Holding(_shared, 3);

        Consume(new ItemReader(library, new[] { _shared }, above).Read());

        Assert.Equal(3, items.Handed);
    }

    /// <summary>
    /// The refusal. A page of no items advances a read by no items, so a reader
    /// handed zero would ask the server for the same nothing forever, and the
    /// symptom is a pass that never ends rather than one that reads nothing.
    /// </summary>
    /// <remarks>
    /// It refuses at construction rather than at the first read, so the mistake
    /// surfaces where the number was chosen. A negative page is the same shape
    /// and is refused by the same comparison.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void AReaderRefusesAPageSmallerThanOneItem(int page)
    {
        var (library, _) = LibraryOfManyItems.Holding(_shared, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemReader(library, new[] { _shared }, page));
    }

    /// <summary>
    /// The neighbour, one item away from the refusal above. A page of one item
    /// is the smallest read that gets anywhere, and it reads the whole library
    /// one item per call rather than being refused.
    /// </summary>
    [Fact]
    public void APageOfOneItemIsARead()
    {
        var (library, items) = LibraryOfManyItems.Holding(_shared, 3);

        var read = new ItemReader(library, new[] { _shared }, 1).Read().Select(item => item.Id).ToList();

        Assert.Equal(Enumerable.Range(0, 3).Select(LibraryOfManyItems.IdentifierAt), read);
        Assert.All(items.AskedForItems.Skip(1), asked => Assert.Equal(1, asked));
        Assert.Equal(3, items.AskedForItems.Count - 1);
    }

    /// <summary>
    /// Reads an answer to the end and keeps none of it, which is what a caller
    /// that only wants the calls made does.
    /// </summary>
    /// <param name="answer">The answer.</param>
    private static void Consume(IEnumerable<BaseItem> answer)
    {
        var counted = 0;

        foreach (var item in answer)
        {
            _ = item;
            counted++;
        }

        Assert.True(counted >= 0, string.Format(CultureInfo.InvariantCulture, "{0} items read", counted));
    }
}
