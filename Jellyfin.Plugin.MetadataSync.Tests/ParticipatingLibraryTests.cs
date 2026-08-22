using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Which libraries a pass may reach, decided before an item is read rather
/// than filtered afterwards.
/// </summary>
/// <remarks>
/// The distinction this file is about is not visible in the answer on a good
/// day. A reader that asks the server for everything and keeps the items whose
/// library is in the participating set returns the same items as one that asks
/// only for the participating libraries, right up until the keeping is wrong,
/// and then a defect in a filter reaches a library an operator explicitly
/// excluded. So the legs below assert the ask as well as the answer, and the
/// proxy answers a query the way the server answers one so that the ask is a
/// thing that can be observed failing.
/// <para>
/// What none of it says is that a pass exists. Nothing in this plugin builds a
/// reader, and the last leg assembles a pass by hand out of the reader, the
/// planner and the applier to say what an item leaving the participating set
/// costs downstream. That is the pieces in the tree held together in a test,
/// not a pass, and #40 is what would run one.
/// </para>
/// </remarks>
public class ParticipatingLibraryTests
{
    private static readonly Guid _shared = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _private = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _sharedFilm = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid _privateFilm = new("44444444-4444-4444-4444-444444444444");

    /// <summary>
    /// The rule. A library that does not take part is not named in what the
    /// reader asks for, so nothing under it is read.
    /// </summary>
    [Fact]
    public void ANonParticipatingLibraryIsNeverEnumerated()
    {
        var (library, items) = LibraryItems.Empty();
        var shared = Film(_sharedFilm, "what the shared library holds");
        var kept = Film(_privateFilm, "what the private library holds");

        items.Put(_shared, shared);
        items.Put(_private, kept);

        var read = new ItemReader(library, new[] { _shared }).Read();

        Assert.Same(shared, Assert.Single(read));
        Assert.NotEmpty(items.AskedFor);
        Assert.All(items.AskedFor, asked => Assert.Equal(new[] { _shared }, asked));
        Assert.DoesNotContain(_private, items.AskedFor.SelectMany(asked => asked));
    }

    /// <summary>
    /// The leg that makes the one above worth reading. The proxy answers an
    /// unbounded query with everything it holds, exactly as the query means, so
    /// a reader that asked for the whole server and narrowed the answer
    /// afterwards would be caught rather than served.
    /// </summary>
    /// <remarks>
    /// Without this the rule above passes on a proxy that quietly answers only
    /// the participating libraries whatever it is asked, which is a test
    /// asserting its own arrangement.
    /// </remarks>
    [Fact]
    public void AQueryThatNamesNoLibraryReadsEverythingTheServerHolds()
    {
        var (library, items) = LibraryItems.Empty();

        items.Put(_shared, Film(_sharedFilm, "what the shared library holds"));
        items.Put(_private, Film(_privateFilm, "what the private library holds"));

        var everything = library.GetItemList(new InternalItemsQuery { Recursive = true });

        Assert.Equal(2, everything.Count);
    }

    /// <summary>
    /// The default a plugin arrives installed with. No library participates, so
    /// the reader asks the server nothing at all, and the empty set is never
    /// turned into the query that means everything.
    /// </summary>
    [Fact]
    public void NothingIsAskedOfTheLibraryWhenNoLibraryParticipates()
    {
        var (library, items) = LibraryItems.Empty();

        items.Put(_shared, Film(_sharedFilm, "what the shared library holds"));

        var read = new ItemReader(library, new PluginConfiguration().ParticipatingLibraries).Read();

        Assert.Empty(read);
        Assert.Empty(items.Called);
    }

    /// <summary>
    /// The set is resolved once. A selection that changes while a pass is
    /// running changes the next pass, so one pass enumerates under one answer
    /// and the result cannot be about two.
    /// </summary>
    [Fact]
    public void TheSetIsTakenAtConstructionAndNotReadAgain()
    {
        var (library, items) = LibraryItems.Empty();
        var shared = Film(_sharedFilm, "what the shared library holds");

        items.Put(_shared, shared);
        items.Put(_private, Film(_privateFilm, "what the private library holds"));

        var chosen = new Collection<Guid> { _shared };
        var reader = new ItemReader(library, chosen);

        chosen.Add(_private);

        Assert.Same(shared, Assert.Single(reader.Read()));
        Assert.Equal(new[] { _shared }, items.AskedFor[^1]);
    }

    /// <summary>
    /// An item moved out of the participating set between two passes is not
    /// written, and the contrast is what says so: the same item, under the same
    /// arrangement, is written on the pass before the move.
    /// </summary>
    /// <remarks>
    /// The write is refused at the read rather than at the write. Nothing later
    /// in the chain is asked about the item at all, which is the property #42
    /// is about: an item that is never enumerated cannot be planned, and a plan
    /// with no row for it cannot carry one to a library.
    /// <para>
    /// The half of this that was already true is a different check and should
    /// not be mistaken for this one. The write path re-reads the item and hands
    /// back a deferral where something else saved it in between, which catches
    /// an item that was touched and not an item that changed library without
    /// otherwise being touched.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AnItemMovedToANonParticipatingLibraryBetweenPassesIsNotWritten()
    {
        var (library, items) = LibraryItems.Empty();
        var film = Film(_sharedFilm, null);

        items.Put(_shared, film);

        var reader = new ItemReader(library, new[] { _shared });

        var before = await Pass(reader).ConfigureAwait(true);

        Assert.Equal(film.Id, Assert.Single(before.Target.Written).LocalItemId);
        Assert.Equal(1, before.Result.ItemsWritten);

        items.Put(_private, film);

        var after = await Pass(reader).ConfigureAwait(true);

        Assert.Empty(after.Target.Written);
        Assert.Equal(0, after.Result.ItemsWritten);
    }

    /// <summary>
    /// A reader built with no library refuses at construction rather than at
    /// the first read, so a mistake surfaces where it was made.
    /// </summary>
    [Fact]
    public void AReaderRefusesAMissingLibrary()
    {
        Assert.Throws<ArgumentNullException>(() => new ItemReader(null!, Array.Empty<Guid>()));
    }

    /// <summary>
    /// The same, for the set. A missing set and an empty one are different
    /// things: one is a pass nobody chose libraries for and the other is a pass
    /// over no libraries.
    /// </summary>
    [Fact]
    public void AReaderRefusesAMissingSet()
    {
        var (library, _) = LibraryItems.Empty();

        Assert.Throws<ArgumentNullException>(() => new ItemReader(library, null!));
    }

    /// <summary>
    /// Runs the reader, the planner and the applier over one another the way a
    /// pass would, and hands back both what was written and what carried it.
    /// </summary>
    /// <param name="reader">The reader the pass is built from.</param>
    /// <returns>The recording target and the applier's own accounting.</returns>
    private static async Task<(RecordingPlanTarget Target, ApplyResult Result)> Pass(ItemReader reader)
    {
        var request = new PlanRequest { Direction = SyncDirection.TwoWay };

        foreach (var item in reader.Read())
        {
            var observed = new ItemObservation
            {
                LocalItemId = item.Id,
                PeerItemId = new Guid("bbbbbbbb-0000-0000-0000-000000000002"),
                Kind = "Movie",
            };

            observed.Fields.Add(new FieldObservation
            {
                Field = "Overview",
                LocalValue = item.Overview,
                PeerValue = "what the peer says about it",
                LastWrittenByThisPlugin = null,
                FieldLockedHere = false,
                FieldLockedOnPeer = false,
            });

            request.Items.Add(observed);
        }

        var target = new RecordingPlanTarget();
        var result = await new Applier(target)
            .ApplyAsync(Planner.Plan(request), CancellationToken.None)
            .ConfigureAwait(true);

        return (target, result);
    }

    /// <summary>
    /// An item of a kind every register row's group holds, so a case about a
    /// library is not accidentally a case about a kind.
    /// </summary>
    /// <param name="id">The identifier the server holds it under.</param>
    /// <param name="overview">What this server says about it.</param>
    /// <returns>The item.</returns>
    private static BaseItem Film(Guid id, string? overview)
    {
        return new Movie { Id = id, Overview = overview };
    }
}
