using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Fields;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The write path, held to calling one member of the server and to refusing
/// every row it cannot spell.
/// </summary>
/// <remarks>
/// Everything here is observed through a library that answers two members and
/// throws on the other seventy-five, so a call this path did not make is as
/// visible as one it did, and a reach for the item repository is a failure with
/// the member's name in it rather than a silent success.
/// <para>
/// What this file cannot show is what the server then does. The double records
/// the request; it runs no metadata saver, writes no sidecar and raises no
/// event. Those consequences are read out of the server's own source in
/// <c>docs/reconciliation.md</c> and are claims about the server rather than
/// anything the suite runs.
/// </para>
/// </remarks>
public class LibraryPlanTargetTests
{
    private static readonly Guid _itemId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// The whole of what a write may ask the server, in order. Held as a field
    /// rather than written at the assertion so the two members are named once.
    /// </summary>
    private static readonly string[] _theWholeConversation = { "GetItemById", "UpdateItemAsync" };

    /// <summary>
    /// The token a freshly built item answers with. Every arrangement here
    /// holds an item nothing has saved, so this is what the plan was made from
    /// and the comparison at the write is satisfied.
    /// </summary>
    private static readonly string _asRead = LibraryPlanTarget.StampOf(new Movie());

    /// <summary>
    /// What the item answers with after something else has saved it, which is
    /// a fixed value rather than a reading of this machine.
    /// </summary>
    private static readonly DateTime _afterSomethingElseSaved = new(2026, 8, 13, 1, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Gets the fields the register lets move that carry a set of strings, so
    /// the refusal is asserted per field rather than once for both.
    /// </summary>
    public static TheoryData<string> SetValuedFields
    {
        get
        {
            var fields = new TheoryData<string>();

            // Named `movable` rather than `field`. From C# 14, which is the
            // language version the newer server line's target framework
            // selects, `field` inside a property accessor binds to a
            // synthesized backing field, so this loop stops compiling there
            // while reading exactly as it did.
            foreach (var movable in LibraryPlanTarget.FieldsWithNoSpelling)
            {
                fields.Add(movable);
            }

            return fields;
        }
    }

    /// <summary>
    /// Gets one case per field this path can write, with the value a plan row
    /// carries and the value the item holds afterwards.
    /// </summary>
    /// <remarks>
    /// The null cases are here rather than in a test of their own because they
    /// are the same question asked of the same writer: a plan row that writes
    /// and carries nothing is the peer holding none, and the field is cleared
    /// rather than left alone. A writer that treated null as "no change" would
    /// keep a value the rules decided against, silently.
    /// </remarks>
    public static TheoryData<string, string?, object?> WritableFieldCases
    {
        get
        {
            var premiere = new DateTime(1979, 5, 25, 9, 30, 0, DateTimeKind.Utc);

            return new TheoryData<string, string?, object?>
            {
                { "Name", "theirs", "theirs" },
                { "Name", null, null },
                { "Overview", "theirs", "theirs" },
                { "Tagline", "theirs", "theirs" },
                { "OfficialRating", "PG", "PG" },
                { "PremiereDate", premiere.ToString("O", CultureInfo.InvariantCulture), premiere },
                { "PremiereDate", null, null },
                { "EndDate", premiere.ToString("O", CultureInfo.InvariantCulture), premiere },
                { "ProductionYear", "1979", 1979 },
                { "ProductionYear", null, null },
            };
        }
    }

    /// <summary>
    /// Every field this path can write is written, and the value that arrives on
    /// the item is the one the row carried.
    /// </summary>
    /// <param name="field">The field, named as the server names it.</param>
    /// <param name="carried">What the plan row carries.</param>
    /// <param name="expected">What the item holds afterwards.</param>
    /// <remarks>
    /// Read back through the property the register says the field is declared
    /// on, rather than through a second table here saying which property each
    /// field is. A second table would be a copy of the writers, and a copy that
    /// agreed with a wrong writer would pass.
    /// </remarks>
    [Theory]
    [MemberData(nameof(WritableFieldCases))]
    public async Task EveryFieldThisPathCanWriteArrivesOnTheItem(string field, string? carried, object? expected)
    {
        var movie = new Movie();
        var (library, _) = LibraryCalls.Holding(_itemId, movie);

        await new LibraryPlanTarget(library).WriteAsync(PlanFor(field, carried), CancellationToken.None);

        var property = typeof(BaseItem).GetProperty(field);
        Assert.NotNull(property);
        Assert.Equal(expected, property.GetValue(movie));
    }

    /// <summary>
    /// Every writer has at least one case above. Without this a writer added
    /// with no case is a line the suite never runs, and the theory would still
    /// be green because it only knows the cases it was given.
    /// </summary>
    [Fact]
    public void EveryWriterHasACase()
    {
        var covered = WritableFieldCases
            .Select(row => (string)row[0]!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(LibraryPlanTarget.WritableFields, field => !covered.Contains(field));
    }

    /// <summary>
    /// The condition read literally. One item written means one call to the one
    /// supported member, and no other member of the library touched at all.
    /// </summary>
    [Fact]
    public async Task AWriteGoesThroughTheSupportedCallAndNothingElse()
    {
        var movie = new Movie();
        var (library, calls) = LibraryCalls.Holding(_itemId, movie);

        await new LibraryPlanTarget(library).WriteAsync(PlanFor("Overview", "theirs"), CancellationToken.None);

        Assert.Equal(_theWholeConversation, calls.Called);
        Assert.Same(movie, Assert.Single(calls.Updates).Item);
        Assert.Equal("theirs", movie.Overview);
    }

    /// <summary>
    /// The update reason is one constant and every write carries it. The
    /// argument for the value is in the constant's own remark and in
    /// <c>docs/reconciliation.md</c>; what is asserted here is that the value
    /// reaches the call and that nothing chose a different one on the way.
    /// </summary>
    [Fact]
    public async Task EveryWriteCarriesTheOneUpdateReason()
    {
        var (library, calls) = LibraryCalls.Holding(_itemId, new Movie());

        await new LibraryPlanTarget(library).WriteAsync(PlanFor("Name", "theirs"), CancellationToken.None);

        Assert.Equal(ItemUpdateType.MetadataEdit, LibraryPlanTarget.UpdateReason);
        Assert.Equal(LibraryPlanTarget.UpdateReason, Assert.Single(calls.Updates).Reason);
    }

    /// <summary>
    /// The token an operator stops a pass with reaches the server call, which
    /// is the last place it can still be honoured.
    /// </summary>
    [Fact]
    public async Task TheTokenReachesTheSupportedCall()
    {
        using var stopping = new CancellationTokenSource();
        var (library, calls) = LibraryCalls.Holding(_itemId, new Movie());

        await new LibraryPlanTarget(library).WriteAsync(PlanFor("Name", "theirs"), stopping.Token);

        Assert.Equal(stopping.Token, Assert.Single(calls.Updates).Token);
    }

    /// <summary>
    /// The item is fetched again at the moment of writing rather than carried
    /// on the plan, so nothing here writes back a copy read at planning time.
    /// The plan holds identifiers and values and no item, and this is the
    /// observation of that.
    /// </summary>
    [Fact]
    public async Task TheItemIsFetchedAgainRatherThanCarriedOnThePlan()
    {
        var (library, calls) = LibraryCalls.Holding(_itemId, new Movie());

        await new LibraryPlanTarget(library).WriteAsync(PlanFor("Name", "theirs"), CancellationToken.None);

        Assert.Equal("GetItemById", calls.Called[0]);
    }

    /// <summary>
    /// A row the plan decided against is not written, and the item is still
    /// handed over for the rows that were. This is the neighbour that keeps the
    /// tests above from passing against a path that writes every row it sees.
    /// </summary>
    [Fact]
    public async Task ARowThePlanDecidedAgainstIsNotWritten()
    {
        var movie = new Movie { Overview = "mine" };
        var (library, _) = LibraryCalls.Holding(_itemId, movie);

        var plan = new ItemPlan { LocalItemId = _itemId, Kind = "Movie", LastSavedWhenPlanned = _asRead };
        plan.Changes.Add(Row("Overview", "theirs", writes: false));
        plan.Changes.Add(Row("Name", "theirs", writes: true));

        await new LibraryPlanTarget(library).WriteAsync(plan, CancellationToken.None);

        Assert.Equal("mine", movie.Overview);
        Assert.Equal("theirs", movie.Name);
    }

    /// <summary>
    /// An item is one unit. A plan whose third row is refused leaves the item
    /// holding what it held, and not the two rows the path had already reached.
    /// </summary>
    /// <remarks>
    /// The refusal tests above each carry one row, so none of them can see this.
    /// What the item holds between the first assignment and the refusal is not a
    /// state either server ever described, and it is the state the next component
    /// to save this item for its own reasons would persist. The refusal stopping
    /// the supported call is not enough on its own, because the object being set
    /// is the library's own item and not a copy of it.
    /// </remarks>
    [Fact]
    public async Task ARefusalPartWayThroughAnItemLeavesTheItemUntouched()
    {
        var movie = new Movie { Name = "mine", Overview = "mine", ProductionYear = 1979 };
        var (library, calls) = LibraryCalls.Holding(_itemId, movie);

        var plan = new ItemPlan { LocalItemId = _itemId, Kind = "Movie", LastSavedWhenPlanned = _asRead };
        plan.Changes.Add(Row("Name", "theirs", writes: true));
        plan.Changes.Add(Row("Overview", "theirs", writes: true));
        plan.Changes.Add(Row("ProductionYear", "nineteen seventy nine", writes: true));

        await Assert.ThrowsAsync<WriteRefusedException>(
            () => new LibraryPlanTarget(library).WriteAsync(plan, CancellationToken.None));

        Assert.Equal("mine", movie.Name);
        Assert.Equal("mine", movie.Overview);
        Assert.Equal(1979, movie.ProductionYear);
        Assert.Empty(calls.Updates);
    }

    /// <summary>
    /// The neighbour to the leg above, and the reason it is not a guard that
    /// refuses everything. The same three rows, all of them readable, all arrive
    /// on the item and the item is handed over once.
    /// </summary>
    [Fact]
    public async Task EveryRowOfAnItemArrivesTogether()
    {
        var movie = new Movie { Name = "mine", Overview = "mine", ProductionYear = 1979 };
        var (library, calls) = LibraryCalls.Holding(_itemId, movie);

        var plan = new ItemPlan { LocalItemId = _itemId, Kind = "Movie", LastSavedWhenPlanned = _asRead };
        plan.Changes.Add(Row("Name", "theirs", writes: true));
        plan.Changes.Add(Row("Overview", "theirs", writes: true));
        plan.Changes.Add(Row("ProductionYear", "1980", writes: true));

        await new LibraryPlanTarget(library).WriteAsync(plan, CancellationToken.None);

        Assert.Equal("theirs", movie.Name);
        Assert.Equal("theirs", movie.Overview);
        Assert.Equal(1980, movie.ProductionYear);
        Assert.Same(movie, Assert.Single(calls.Updates).Item);
    }

    /// <summary>
    /// A value the peer does not hold clears the field rather than being passed
    /// over. The plan says to write and carries nothing, which is the peer
    /// holding none, and leaving the old value in place would be this server
    /// keeping a value the rules decided against.
    /// </summary>
    [Fact]
    public async Task AValueThePeerDoesNotHoldClearsTheField()
    {
        var movie = new Movie { Overview = "mine" };
        var (library, _) = LibraryCalls.Holding(_itemId, movie);

        await new LibraryPlanTarget(library).WriteAsync(PlanFor("Overview", null), CancellationToken.None);

        Assert.Null(movie.Overview);
    }

    /// <summary>
    /// A date is read in the round-trip spelling and in no other. The value
    /// asserted here is the one <c>DateTime.ToString("O")</c> produces, so the
    /// spelling this path reads is the spelling a writer gets by asking for the
    /// round trip rather than one somebody has to look up.
    /// </summary>
    [Fact]
    public async Task ADateIsReadInTheRoundTripSpelling()
    {
        var movie = new Movie();
        var (library, _) = LibraryCalls.Holding(_itemId, movie);
        var premiere = new DateTime(1979, 5, 25, 9, 30, 0, DateTimeKind.Utc);

        await new LibraryPlanTarget(library).WriteAsync(
            PlanFor("PremiereDate", premiere.ToString("O", CultureInfo.InvariantCulture)),
            CancellationToken.None);

        Assert.Equal(premiere, movie.PremiereDate);
        Assert.Equal(DateTimeKind.Utc, movie.PremiereDate!.Value.Kind);
    }

    /// <summary>
    /// A date in any other spelling is refused rather than read under whatever
    /// locale the machine happens to have. The value used is the one that is
    /// dangerous rather than the one that is obviously wrong: it parses on both
    /// of two machines and means a different month on each.
    /// </summary>
    [Fact]
    public async Task ADateInAnotherSpellingIsRefused()
    {
        var (library, calls) = LibraryCalls.Holding(_itemId, new Movie());

        var refusal = await Assert.ThrowsAsync<WriteRefusedException>(
            () => new LibraryPlanTarget(library).WriteAsync(PlanFor("PremiereDate", "05/06/1979"), CancellationToken.None));

        Assert.Contains("PremiereDate", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(calls.Updates);
    }

    /// <summary>
    /// A year that is a plain number is written. This is the neighbour the
    /// refusal below is judged against, and it is also where the spelling is
    /// stated: digits, no sign, no thousands separator and no space.
    /// </summary>
    [Fact]
    public async Task AYearThatIsANumberIsWritten()
    {
        var movie = new Movie();
        var (library, _) = LibraryCalls.Holding(_itemId, movie);

        await new LibraryPlanTarget(library).WriteAsync(PlanFor("ProductionYear", "1979"), CancellationToken.None);

        Assert.Equal(1979, movie.ProductionYear);
    }

    /// <summary>
    /// A year that is not a plain number is refused. Nothing is written, which
    /// is the half that matters: a row this path cannot read leaves the item as
    /// it was rather than half changed.
    /// </summary>
    [Fact]
    public async Task AYearThatIsNotANumberIsRefused()
    {
        var (library, calls) = LibraryCalls.Holding(_itemId, new Movie());

        await Assert.ThrowsAsync<WriteRefusedException>(
            () => new LibraryPlanTarget(library).WriteAsync(PlanFor("ProductionYear", "nineteen seventy nine"), CancellationToken.None));

        Assert.Empty(calls.Updates);
    }

    /// <summary>
    /// A field the register lets move that the server holds as a set of strings
    /// is refused, and the message says why rather than saying the field is
    /// unknown. One string cannot carry a set without a declared escaping, and
    /// nothing declares one.
    /// </summary>
    /// <param name="field">The set-valued field.</param>
    [Theory]
    [MemberData(nameof(SetValuedFields))]
    public async Task AFieldThatCarriesASetIsRefused(string field)
    {
        var (library, calls) = LibraryCalls.Holding(_itemId, new Movie());

        var refusal = await Assert.ThrowsAsync<WriteRefusedException>(
            () => new LibraryPlanTarget(library).WriteAsync(PlanFor(field, "one, two"), CancellationToken.None));

        Assert.Contains(field, refusal.Message, StringComparison.Ordinal);
        Assert.Empty(calls.Updates);
    }

    /// <summary>
    /// A field this path has no writer for is refused rather than passed over.
    /// A row naming a field nothing can write is a plan made against a different
    /// register, and writing the rest of the item as though it were complete is
    /// the failure this refusal exists for.
    /// </summary>
    [Fact]
    public async Task AFieldWithNoWriterIsRefused()
    {
        var (library, calls) = LibraryCalls.Holding(_itemId, new Movie());

        await Assert.ThrowsAsync<WriteRefusedException>(
            () => new LibraryPlanTarget(library).WriteAsync(PlanFor("SortName", "theirs"), CancellationToken.None));

        Assert.Empty(calls.Updates);
    }

    /// <summary>
    /// An item that has gone between the plan and the write is refused with a
    /// type of its own, because it is the one refusal here that is nobody's
    /// defect and the one #41 turns into a deferral.
    /// </summary>
    [Fact]
    public async Task AnItemThatIsNotInTheLibraryIsRefused()
    {
        var (library, calls) = LibraryCalls.Empty();

        await Assert.ThrowsAsync<ItemNotInLibraryException>(
            () => new LibraryPlanTarget(library).WriteAsync(PlanFor("Name", "theirs"), CancellationToken.None));

        Assert.Empty(calls.Updates);
    }

    /// <summary>
    /// Something else wrote the item between the plan and the write, so nothing
    /// on it is written and the item is handed back as deferred. This is the
    /// guard for the failure an operator sees as a field flipping back and forth
    /// between a sync and a refresh, with no log explaining it.
    /// </summary>
    [Fact]
    public async Task AnItemSomethingElseWroteSinceThePlanIsDeferred()
    {
        var movie = new Movie { Overview = "what a refresh just wrote" };
        var (library, calls) = LibraryCalls.Holding(_itemId, movie);

        // The plan below was made from the item as it was read. The library now
        // holds a version something else saved after that.
        movie.DateLastSaved = _afterSomethingElseSaved;

        await Assert.ThrowsAsync<ItemChangedSincePlannedException>(
            () => new LibraryPlanTarget(library).WriteAsync(PlanFor("Overview", "theirs"), CancellationToken.None));

        Assert.Equal("what a refresh just wrote", movie.Overview);
        Assert.Empty(calls.Updates);
    }

    /// <summary>
    /// A plan that carries no token cannot answer whether the item moved, and a
    /// write made without the answer is what this guard exists to stop. It is
    /// refused as a defect rather than deferred, because nothing about it will
    /// be different on the next pass.
    /// </summary>
    [Fact]
    public async Task APlanThatCarriesNoTokenIsRefused()
    {
        var (library, calls) = LibraryCalls.Holding(_itemId, new Movie());

        var plan = new ItemPlan { LocalItemId = _itemId, Kind = "Movie" };
        plan.Changes.Add(Row("Overview", "theirs", writes: true));

        await Assert.ThrowsAsync<WriteRefusedException>(
            () => new LibraryPlanTarget(library).WriteAsync(plan, CancellationToken.None));

        Assert.Empty(calls.Updates);
    }

    /// <summary>
    /// The token is derived in one place, so the half that reads items and the
    /// half that writes them cannot spell it two ways. A token that never
    /// changed would defer nothing and one that always changed would defer
    /// everything, so both directions are asserted.
    /// </summary>
    [Fact]
    public void TheTokenChangesWhenTheItemIsSavedAndNotOtherwise()
    {
        var movie = new Movie();

        Assert.Equal(LibraryPlanTarget.StampOf(movie), LibraryPlanTarget.StampOf(new Movie()));

        movie.DateLastSaved = _afterSomethingElseSaved;

        Assert.NotEqual(_asRead, LibraryPlanTarget.StampOf(movie));
    }

    /// <summary>
    /// Deriving a token from an item that is not there is refused, rather than
    /// answering with a token that would compare equal to another absence.
    /// </summary>
    [Fact]
    public void TakingATokenFromAnItemThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => LibraryPlanTarget.StampOf(null!));
    }

    /// <summary>
    /// A target with no library is refused when it is built, because a target
    /// that exists and cannot write is a pass reporting that it applied a plan.
    /// </summary>
    [Fact]
    public void ATargetWithNoLibraryIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new LibraryPlanTarget(null!));
    }

    /// <summary>
    /// Writing nothing is refused as it is called rather than when it is
    /// awaited, for the reason the applier splits the same way.
    /// </summary>
    [Fact]
    public void WritingAnItemPlanThatIsNotThereIsRefused()
    {
        var (library, _) = LibraryCalls.Empty();
        var target = new LibraryPlanTarget(library);

        // A statement body rather than an expression, so this is an action the
        // assertion runs and not a task it is handed. The refusal is the point:
        // it arrives as the call is made rather than whenever somebody awaits.
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = target.WriteAsync(null!, CancellationToken.None);
        });
    }

    /// <summary>
    /// The two sets this path declares cover exactly the fields the register
    /// lets move, and they do not overlap. This is the leg that keeps the
    /// refusal above from becoming a quiet gap: a tenth row declared to move
    /// reds the suite until somebody decides whether it can be spelled in a
    /// plan row, and a writer for a field the register refuses reds it too.
    /// </summary>
    [Fact]
    public void TheWritersAndTheRefusedFieldsAreExactlyWhatTheRegisterLetsMove()
    {
        var moving = FieldRegister.Rows
            .Where(row => row.Moves)
            .Select(row => row.Field)
            .Order(StringComparer.Ordinal)
            .ToList();

        var covered = LibraryPlanTarget.WritableFields
            .Concat(LibraryPlanTarget.FieldsWithNoSpelling)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(moving, covered);
        Assert.Empty(LibraryPlanTarget.WritableFields.Intersect(LibraryPlanTarget.FieldsWithNoSpelling, StringComparer.Ordinal));
    }

    /// <summary>
    /// The double is doing what the tests above assume. A library that answered
    /// every member quietly would let a path reaching for the item repository
    /// pass, so the refusal it is built around is asserted rather than trusted.
    /// </summary>
    [Fact]
    public void TheLibraryDoubleRefusesEveryMemberButTheTwo()
    {
        var (library, _) = LibraryCalls.Empty();

        Assert.Throws<NotSupportedException>(() => library.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery()));
    }

    private static ItemPlan PlanFor(string field, string? value)
    {
        var plan = new ItemPlan { LocalItemId = _itemId, Kind = "Movie", LastSavedWhenPlanned = _asRead };
        plan.Changes.Add(Row(field, value, writes: true));
        return plan;
    }

    private static PlannedChange Row(string field, string? value, bool writes)
    {
        return new PlannedChange
        {
            Field = field,
            PeerValue = value,
            Writes = writes,
            ValueToWrite = writes ? value : null,
            Reason = "arranged in a test",
        };
    }
}
