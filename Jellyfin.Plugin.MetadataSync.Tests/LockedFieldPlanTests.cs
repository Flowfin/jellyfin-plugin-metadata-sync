using System;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Fields;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Every lock the server declares, held against the code that decides whether a
/// field is written.
/// </summary>
/// <remarks>
/// This is the only place the nine names are held. They were covered a second
/// time in <see cref="FieldRegisterTests"/>, through a type that wrote a field
/// on a call no pass made, and that was a different statement rather than a
/// stronger one: <see cref="Planner"/> decides and <see cref="Applier"/> obeys
/// the flag the decision left on the row without asking the register, the rules
/// or a lock a second time, so a lock proved on that other call would have gone
/// on reading as a working guard after the deciding half stopped honouring it.
/// The type is gone and the second copy with it.
/// <para>
/// The sweep is over the server's own enumeration rather than over nine names
/// written here, so a tenth lockable field on a later server line is inside the
/// subject on the day the reference moves rather than on the day somebody
/// remembers this file. Which register row each lock governs is looked up from
/// the register for the same reason; that exactly one row answers to each lock
/// is <see cref="FieldRegisterTests.EachOfTheNineLockableNamesGovernsExactlyOneRow"/>
/// and is depended on here rather than asserted a second time.
/// </para>
/// <para>
/// Four of the nine govern a row the register refuses to move at all, so the
/// register answers before a lock is read. That is a stronger refusal rather
/// than a gap, and the sweep says which of the two each name met instead of
/// accepting both silently: a row that stops refusing to move, and whose lock
/// is then not honoured, would otherwise slide from one clean answer to
/// another with nothing reddening.
/// </para>
/// <para>
/// What none of this reaches. A lock is read once, while the plan is made, so a
/// lock an operator sets between the plan and the write is not seen here; what
/// narrows that window is the deferral in <see cref="DeferralTests"/> and
/// <c>docs/reconciliation.md</c> says it is narrowed rather than closed. And a
/// peer's lock refuses a change on this side only. There is no send in this
/// tree, so nothing here says a field the peer reported as locked is not sent
/// to them.
/// </para>
/// </remarks>
public class LockedFieldPlanTests
{
    /// <summary>
    /// An item kind every group in the register contains, so a case about a
    /// lock is not accidentally a case about a kind.
    /// </summary>
    private const string AKindEveryGroupHolds = "Movie";

    /// <summary>
    /// Gets every lock the server declares, named as the server names it.
    /// </summary>
    public static TheoryData<string> EveryLockTheServerDeclares
    {
        get
        {
            var locks = new TheoryData<string>();
            foreach (var name in Enum.GetNames<MetadataField>())
            {
                locks.Add(name);
            }

            return locks;
        }
    }

    /// <summary>
    /// The theories below are one case per name in the server's enumeration,
    /// and each of them looks the name up in the register. An enumeration that
    /// came back empty would run no case at all and every theory would pass, so
    /// the population is asserted before anything is concluded from them.
    /// </summary>
    [Fact]
    public void EveryLockTheServerDeclaresIsOneTheRegisterAnswersFor()
    {
        var declared = Enum.GetNames<MetadataField>();

        Assert.NotEmpty(declared);
        Assert.Equal(declared.Length, FieldRegister.Rows.Count(row => row.Lock is not null));

        foreach (var name in declared)
        {
            Assert.NotNull(RowGovernedBy(name));
        }
    }

    /// <summary>
    /// A field the operator locked here is not written, for every lock the
    /// server has. The arrangement is the one that writes when no lock is
    /// claimed, which is the neighbour below.
    /// </summary>
    /// <param name="declaredLock">The lock, from the server's enumeration.</param>
    [Theory]
    [MemberData(nameof(EveryLockTheServerDeclares))]
    public void ALockedFieldIsNotWritten(string declaredLock)
    {
        var row = RowGovernedBy(declaredLock);

        var change = OnlyChangeIn(Locked(row.Field, here: true));

        Assert.False(change.Writes);
        Assert.Null(change.ValueToWrite);

        if (row.Moves)
        {
            Assert.Equal(PlanDisposition.Decided, change.Disposition);
            Assert.Equal(ConflictOutcome.KeepLocal, change.Outcome);
            Assert.Equal("field-locked-here", change.Rule);
        }
        else
        {
            // The register refuses first, so the lock is never read. Saying
            // which of the two answered is what keeps a row that later starts
            // moving from arriving here as an unremarked pass.
            Assert.Equal(PlanDisposition.DoesNotMove, change.Disposition);
            Assert.Null(change.Rule);
        }
    }

    /// <summary>
    /// The same nine arrangements with no lock claimed. Every row the register
    /// moves is written, which is what makes the refusals above a property of
    /// the lock rather than of a planner that writes nothing.
    /// </summary>
    /// <param name="declaredLock">The lock, from the server's enumeration.</param>
    [Theory]
    [MemberData(nameof(EveryLockTheServerDeclares))]
    public void TheSameFieldWithNoLockClaimedIsWrittenWhereTheRegisterMovesIt(string declaredLock)
    {
        var row = RowGovernedBy(declaredLock);

        var change = OnlyChangeIn(Locked(row.Field, here: false));

        Assert.Equal(row.Moves, change.Writes);

        if (row.Moves)
        {
            Assert.Equal("local-value-absent", change.Rule);
            Assert.Equal("theirs", change.ValueToWrite);
        }
    }

    /// <summary>
    /// The peer's claim on the same nine names refuses the change here, and
    /// neither value is written.
    /// </summary>
    /// <remarks>
    /// The values have to differ for this rule to be the one that answers. An
    /// arrangement where this server holds nothing is taken from the peer
    /// before their lock is reached, and that is correct rather than a hole:
    /// their lock is a claim on what is written to them, and taking their value
    /// writes nothing there.
    /// </remarks>
    /// <param name="declaredLock">The lock, from the server's enumeration.</param>
    [Theory]
    [MemberData(nameof(EveryLockTheServerDeclares))]
    public void AFieldTheOtherServerLockedIsNotOverwrittenHereEither(string declaredLock)
    {
        var row = RowGovernedBy(declaredLock);

        var observation = new FieldObservation
        {
            Field = row.Field,
            LocalValue = "ours",
            PeerValue = "theirs",
            LastWrittenByThisPlugin = null,
            FieldLockedHere = false,
            FieldLockedOnPeer = true,
        };

        var change = OnlyChangeIn(observation);

        Assert.False(change.Writes);

        if (row.Moves)
        {
            Assert.Equal(ConflictOutcome.Refuse, change.Outcome);
            Assert.Equal("peer-field-locked", change.Rule);
        }
    }

    /// <summary>
    /// An item carrying the server's item-level lock is not written at all, over
    /// every field the register declares rather than over three of them.
    /// </summary>
    [Fact]
    public void NothingIsWrittenToALockedItemForAnyFieldTheRegisterDeclares()
    {
        var plan = Planner.Plan(RequestOverTheWholeRegister(itemLocked: true));

        Assert.Equal(0, plan.FieldsToWrite);
        Assert.Equal(FieldRegister.Rows.Count, plan.FieldsConsidered);

        foreach (var change in Assert.Single(plan.Items).Changes)
        {
            Assert.False(change.Writes);

            if (MovesOnThisKind(change.Field))
            {
                Assert.Equal("item-locked-here", change.Rule);
            }
        }
    }

    /// <summary>
    /// The same request with the item unlocked. Every row the register moves for
    /// this kind is written, so the count above is a refusal rather than an
    /// arrangement in which nothing was ever going to be written.
    /// </summary>
    [Fact]
    public void TheSameItemUnlockedIsWrittenForEveryRowTheRegisterMoves()
    {
        var expected = FieldRegister.Rows.Count(MovesOnThisKind);

        var plan = Planner.Plan(RequestOverTheWholeRegister(itemLocked: false));

        Assert.True(expected > 0, "The register moves no field for this kind, so the neighbour proves nothing.");
        Assert.Equal(expected, plan.FieldsToWrite);
    }

    /// <summary>
    /// The register row the given lock governs.
    /// </summary>
    private static FieldRow RowGovernedBy(string declaredLock)
    {
        return FieldRegister.Rows.Single(
            row => row.Lock is not null
                && string.Equals(row.Lock.Value.ToString(), declaredLock, StringComparison.Ordinal));
    }

    /// <summary>
    /// An arrangement this server holds nothing for, which the rules answer by
    /// taking the peer's value unless something refuses first.
    /// </summary>
    private static FieldObservation Locked(string field, bool here)
    {
        return new FieldObservation
        {
            Field = field,
            LocalValue = null,
            PeerValue = "theirs",
            LastWrittenByThisPlugin = null,
            FieldLockedHere = here,
            FieldLockedOnPeer = false,
        };
    }

    private static bool MovesOnThisKind(string field)
    {
        var row = FieldRegister.Find(field);

        return row is not null
            && row.Moves
            && row.Kinds.Contains(AKindEveryGroupHolds, StringComparer.Ordinal);
    }

    private static bool MovesOnThisKind(FieldRow row) => MovesOnThisKind(row.Field);

    private static PlannedChange OnlyChangeIn(FieldObservation observation)
    {
        var item = NewItem(itemLocked: false);
        item.Fields.Add(observation);

        return Assert.Single(Assert.Single(Planner.Plan(RequestFor(item)).Items).Changes);
    }

    private static PlanRequest RequestOverTheWholeRegister(bool itemLocked)
    {
        var item = NewItem(itemLocked);

        foreach (var row in FieldRegister.Rows)
        {
            item.Fields.Add(Locked(row.Field, here: false));
        }

        return RequestFor(item);
    }

    private static ItemObservation NewItem(bool itemLocked)
    {
        return new ItemObservation
        {
            LocalItemId = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
            PeerItemId = new Guid("bbbbbbbb-0000-0000-0000-000000000002"),
            Kind = AKindEveryGroupHolds,
            ItemLockedHere = itemLocked,
        };
    }

    private static PlanRequest RequestFor(ItemObservation item)
    {
        var request = new PlanRequest { Direction = SyncDirection.TwoWay };
        request.Items.Add(item);
        return request;
    }
}
