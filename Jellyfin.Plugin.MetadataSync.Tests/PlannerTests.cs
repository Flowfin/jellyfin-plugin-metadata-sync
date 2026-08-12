using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Fields;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The planner, run over tables. Nothing below constructs a server, a database,
/// a request or a clock, and none of it substitutes one either: a case is
/// arranged by writing down what two servers hold, which is the property this
/// file exists to hold rather than to claim.
/// </summary>
/// <remarks>
/// Two kinds of test are here and they prove different things. The rule tests
/// hand the planner one arrangement and read the row that comes back, so each
/// declared rule in the register and in the conflict table is seen answering
/// through the planner rather than only through the type that owns it. The
/// shape tests read the planner's own input types by reflection, so a
/// dependency added later fails here instead of being noticed in review.
/// </remarks>
public class PlannerTests
{
    /// <summary>
    /// An item kind every register row's group contains, so a case about a
    /// field is not accidentally a case about a kind.
    /// </summary>
    private const string AKindEveryGroupHolds = "Movie";

    /// <summary>
    /// Gets the name of every case in the conflict document's fixture table.
    /// </summary>
    public static TheoryData<string> ConflictCaseNames => ConflictFixtures.CaseNames;

    /// <summary>
    /// Gets every field the register declares, so the sweep below is over the
    /// register rather than over a list somebody kept up to date.
    /// </summary>
    public static TheoryData<string> EveryDeclaredField
    {
        get
        {
            var fields = new TheoryData<string>();
            foreach (var row in FieldRegister.Rows)
            {
                fields.Add(row.Field);
            }

            return fields;
        }
    }

    /// <summary>
    /// Gets every field the register declares as one that does not move. These
    /// are the rows every rule in M2 lands in: the image fields, the per-user
    /// fields, the reference fields and the fields derived from the file.
    /// </summary>
    public static TheoryData<string> EveryFieldThatDoesNotMove
    {
        get
        {
            var fields = new TheoryData<string>();
            foreach (var row in FieldRegister.Rows.Where(row => !row.Moves))
            {
                fields.Add(row.Field);
            }

            return fields;
        }
    }

    /// <summary>
    /// The planner takes values and returns values, and this is the whole
    /// arrangement: two strings, a field name and a kind. There is no
    /// substitute anywhere in it because there is nothing to substitute.
    /// </summary>
    [Fact]
    public void APlanIsMadeFromValuesAndNothingElse()
    {
        var plan = Planner.Plan(RequestFor(Observed("Overview", local: "ours", peer: "theirs")));

        var change = Assert.Single(Assert.Single(plan.Items).Changes);

        Assert.Equal(PlanDisposition.Decided, change.Disposition);
        Assert.Equal(ConflictOutcome.Refuse, change.Outcome);
        Assert.False(change.Writes);
    }

    /// <summary>
    /// The planner holds nothing. A static class with no state cannot be handed
    /// a library in a constructor, which is the shape the condition asks for
    /// read as a property of the type rather than as a habit of its callers.
    /// </summary>
    [Fact]
    public void ThePlannerHoldsNothingItCouldHaveBeenGiven()
    {
        Assert.True(typeof(Planner).IsAbstract && typeof(Planner).IsSealed, "The planner is not a static class.");

        Assert.Empty(typeof(Planner).GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
    }

    /// <summary>
    /// The four dependencies the condition names, refused at the input surface
    /// rather than at the call sites. A planner that takes no server type, no
    /// database type, no transport type and no clock cannot reach one, however
    /// its body is written later.
    /// </summary>
    /// <remarks>
    /// This walks the types reachable from the request rather than the planner's
    /// own body, because the body is what a later edit changes and the input
    /// surface is what a later edit would have to widen first. What it cannot
    /// catch is a dependency reached through a plain string, for instance a
    /// connection string or a path handed in as a value, and a type that names
    /// none of the four families below.
    /// </remarks>
    [Fact]
    public void NothingReachableFromTheRequestIsAServerADatabaseATransportOrAClock()
    {
        var reachable = new HashSet<Type>();
        Walk(typeof(PlanRequest), reachable);

        var refused = reachable
            .Where(type => IsAServerType(type) || IsADatabaseType(type) || IsATransportType(type) || IsAClock(type))
            .Select(type => type.FullName ?? type.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(refused.Count == 0, "The planner's input surface reaches: " + string.Join(", ", refused));
    }

    /// <summary>
    /// The walk reaches something. A reflection test over an empty set passes
    /// for the wrong reason, so the set is asserted before anything is
    /// concluded from it being clean.
    /// </summary>
    [Fact]
    public void TheWalkOverTheRequestReachesTheTypesItIsAbout()
    {
        var reachable = new HashSet<Type>();
        Walk(typeof(PlanRequest), reachable);

        Assert.Contains(typeof(ItemObservation), reachable);
        Assert.Contains(typeof(FieldObservation), reachable);
        Assert.Contains(typeof(SyncDirection), reachable);
    }

    /// <summary>
    /// A field with no row is not written, whatever the two servers hold. This
    /// is the register's first refusal seen through the planner: nothing knows
    /// what a wrong value in an undeclared field costs, so nothing may write
    /// one.
    /// </summary>
    [Fact]
    public void AFieldTheRegisterDoesNotDeclareIsNotWritten()
    {
        var plan = Planner.Plan(RequestFor(Observed("NoSuchField", local: null, peer: "theirs")));

        var change = Assert.Single(Assert.Single(plan.Items).Changes);

        Assert.Equal(PlanDisposition.NotDeclared, change.Disposition);
        Assert.Null(change.Outcome);
        Assert.False(change.Writes);
        Assert.Contains("NoSuchField", change.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every row the register declares as one that does not move is refused by
    /// the planner, and refused before the conflict rules are asked. This is
    /// every rule in M2 that is a row: the image fields from #14, the per-user
    /// fields from #18, the reference fields from #15 and the fields derived
    /// from this server's own file.
    /// </summary>
    /// <param name="field">The field, from the register.</param>
    [Theory]
    [MemberData(nameof(EveryFieldThatDoesNotMove))]
    public void AFieldTheRegisterRefusesToMoveIsNotWrittenAndNeverReachesTheRules(string field)
    {
        var row = FieldRegister.Find(field);
        Assert.NotNull(row);

        var plan = Planner.Plan(RequestFor(
            Observed(field, local: "ours", peer: "theirs"),
            kind: row.Kinds[0]));

        var change = Assert.Single(Assert.Single(plan.Items).Changes);

        Assert.Equal(PlanDisposition.DoesNotMove, change.Disposition);
        Assert.Null(change.Outcome);
        Assert.Null(change.Rule);
        Assert.False(change.Writes);
        Assert.Equal(row.OperatorReason, change.Reason);
    }

    /// <summary>
    /// A row is about the kinds its group names and about nothing else. The
    /// tagline row is declared for video kinds, and a book is not one of them,
    /// so the answer is that the row does not apply rather than that the field
    /// was refused.
    /// </summary>
    [Fact]
    public void AFieldDeclaredForOtherKindsDoesNotApplyToThisOne()
    {
        var plan = Planner.Plan(RequestFor(
            Observed("Tagline", local: null, peer: "theirs"),
            kind: "Book"));

        var change = Assert.Single(Assert.Single(plan.Items).Changes);

        Assert.Equal(PlanDisposition.OutsideTheKindGroup, change.Disposition);
        Assert.False(change.Writes);
        Assert.Contains("Book", change.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// A collection and a playlist are kinds no group names, so a pass over one
    /// writes nothing to it and says so per field rather than failing. That is
    /// the scope decision in #19 held at the planner instead of only in the
    /// document that declares it.
    /// </summary>
    /// <param name="kind">The kind, as the server names the type.</param>
    [Theory]
    [InlineData("BoxSet")]
    [InlineData("Playlist")]
    public void NoFieldAppliesToACollectionOrAPlaylist(string kind)
    {
        var request = RequestFor(Observed("Name", local: "ours", peer: "theirs"), kind);
        foreach (var row in FieldRegister.Rows)
        {
            request.Items[0].Fields.Add(Observed(row.Field, local: "ours", peer: "theirs"));
        }

        var plan = Planner.Plan(request);

        Assert.All(
            Assert.Single(plan.Items).Changes,
            change => Assert.Equal(PlanDisposition.OutsideTheKindGroup, change.Disposition));
        Assert.Equal(0, plan.FieldsToWrite);
    }

    /// <summary>
    /// The operator's exclusion subtracts from what the register allows. It is
    /// asked after the register and before the rules, so an excluded field is
    /// answered as excluded rather than as a difference nobody resolved.
    /// </summary>
    [Fact]
    public void AFieldTheOperatorExcludedIsNotWritten()
    {
        var request = RequestFor(Observed("Overview", local: null, peer: "theirs"));
        request.ExcludedFields.Add("Overview");

        var change = Assert.Single(Assert.Single(Planner.Plan(request).Items).Changes);

        Assert.Equal(PlanDisposition.ExcludedByTheOperator, change.Disposition);
        Assert.Null(change.Outcome);
        Assert.False(change.Writes);
    }

    /// <summary>
    /// The same field, not excluded, is written. This is the neighbour that
    /// keeps the test above from passing against a planner that writes nothing
    /// at all.
    /// </summary>
    [Fact]
    public void TheSameFieldWithNoExclusionIsWritten()
    {
        var change = Assert.Single(Assert.Single(
            Planner.Plan(RequestFor(Observed("Overview", local: null, peer: "theirs"))).Items).Changes);

        Assert.Equal(PlanDisposition.Decided, change.Disposition);
        Assert.Equal(ConflictOutcome.TakePeer, change.Outcome);
        Assert.True(change.Writes);
        Assert.Equal("theirs", change.ValueToWrite);
    }

    /// <summary>
    /// Every case the conflict document argues its rule set from, answered
    /// through the planner and compared against what the document declares.
    /// </summary>
    /// <remarks>
    /// This is the condition that every rule in M6 is testable against the
    /// planner alone, read as a sweep rather than as a rule-by-rule claim: the
    /// cases come out of the document, the expected answers were fixed before
    /// any of this code existed, and a planner that reordered the table or
    /// answered a case itself would disagree with one of them.
    /// </remarks>
    /// <param name="caseName">The case, named by the document.</param>
    [Theory]
    [MemberData(nameof(ConflictCaseNames))]
    public void EveryConflictCaseIsAnsweredThroughThePlannerAsTheDocumentDeclares(string caseName)
    {
        var row = ConflictFixtures.Named(caseName);
        var inputs = ConflictFixtures.InputsFor(row);

        var change = Assert.Single(Assert.Single(Planner.Plan(RequestFor(FromInputs(inputs), AKindEveryGroupHolds, inputs.ItemLockedHere)).Items).Changes);

        Assert.Equal(PlanDisposition.Decided, change.Disposition);
        Assert.Equal(ConflictFixtures.OutcomeFor(row), change.Outcome);
        Assert.Equal(ConflictFixtures.RuleIdFor(row), change.Rule);
    }

    /// <summary>
    /// A lock claim only decides where the register says a lock governs the
    /// field. The tagline row names no server lock, so a side reporting that
    /// lock as set says nothing about it, and the item-level lock is the only
    /// instrument left.
    /// </summary>
    [Fact]
    public void ALockClaimedForAFieldNoLockGovernsDecidesNothing()
    {
        var observation = new FieldObservation
        {
            Field = "Tagline",
            LocalValue = "ours",
            PeerValue = "theirs",
            LastWrittenByThisPlugin = null,
            FieldLockedHere = true,
            FieldLockedOnPeer = true,
        };

        var change = Assert.Single(Assert.Single(Planner.Plan(RequestFor(observation)).Items).Changes);

        Assert.Equal(ConflictOutcome.Refuse, change.Outcome);
        Assert.Null(change.Rule);
    }

    /// <summary>
    /// The same claim on a field the register does govern by a lock is a lock.
    /// This is the neighbour of the test above and it differs by the field
    /// name alone.
    /// </summary>
    [Fact]
    public void TheSameClaimOnAFieldALockGovernsKeepsTheLocalValue()
    {
        var observation = new FieldObservation
        {
            Field = "Overview",
            LocalValue = "ours",
            PeerValue = "theirs",
            LastWrittenByThisPlugin = null,
            FieldLockedHere = true,
            FieldLockedOnPeer = false,
        };

        var change = Assert.Single(Assert.Single(Planner.Plan(RequestFor(observation)).Items).Changes);

        Assert.Equal(ConflictOutcome.KeepLocal, change.Outcome);
        Assert.Equal("field-locked-here", change.Rule);
    }

    /// <summary>
    /// An item the operator locked writes nothing, whatever is on it. The rule
    /// is the conflict table's first row and the planner hands it the item's
    /// answer, so a plan over a locked item is a plan that writes nothing.
    /// </summary>
    [Fact]
    public void NothingIsWrittenToAnItemTheOperatorLocked()
    {
        var request = RequestFor(Observed("Overview", local: null, peer: "theirs"), AKindEveryGroupHolds, itemLocked: true);
        request.Items[0].Fields.Add(Observed("Name", local: null, peer: "theirs"));
        request.Items[0].Fields.Add(Observed("Tags", local: null, peer: "theirs"));

        var plan = Planner.Plan(request);

        Assert.Equal(0, plan.FieldsToWrite);
        Assert.All(
            Assert.Single(plan.Items).Changes,
            change => Assert.Equal("item-locked-here", change.Rule));
    }

    /// <summary>
    /// The answer for every field is in the plan, in the order it was
    /// considered, including the fields nothing was done about. A field missing
    /// from a plan and a field nobody looked at are the same thing to whoever
    /// reads it, so the planner leaves nothing out.
    /// </summary>
    /// <param name="field">The field, from the register.</param>
    [Theory]
    [MemberData(nameof(EveryDeclaredField))]
    public void EveryFieldConsideredHasARowInThePlan(string field)
    {
        var request = RequestFor(Observed(field, local: "ours", peer: "theirs"));
        request.Items[0].Fields.Add(Observed("NoSuchField", local: "ours", peer: "theirs"));

        var changes = Assert.Single(Planner.Plan(request).Items).Changes;

        Assert.Equal(2, changes.Count);
        Assert.Equal(field, changes[0].Field);
        Assert.Equal("NoSuchField", changes[1].Field);
    }

    /// <summary>
    /// The flag the applier obeys agrees with the decision it came from,
    /// everywhere. It is stored rather than derived so that the applier cannot
    /// re-derive it, and a stored answer is one that can disagree with its
    /// source, so the suite holds the two together instead of trusting them.
    /// </summary>
    /// <param name="caseName">The case, named by the conflict document.</param>
    [Theory]
    [MemberData(nameof(ConflictCaseNames))]
    public void WhatThePlanSaysItWritesAgreesWithWhatTheRulesDecided(string caseName)
    {
        var inputs = ConflictFixtures.InputsFor(ConflictFixtures.Named(caseName));

        var change = Assert.Single(Assert.Single(Planner.Plan(RequestFor(FromInputs(inputs), AKindEveryGroupHolds, inputs.ItemLockedHere)).Items).Changes);

        Assert.Equal(change.Outcome == ConflictOutcome.TakePeer, change.Writes);

        if (change.Writes)
        {
            Assert.Equal(change.PeerValue, change.ValueToWrite);
        }
        else
        {
            Assert.Null(change.ValueToWrite);
        }
    }

    /// <summary>
    /// The plan carries the direction it was made under. An entry saying a
    /// field was not written is unreadable without it, and looking it up again
    /// later would be reading a value that may since have changed.
    /// </summary>
    [Fact]
    public void ThePlanCarriesThePairingAndTheDirectionItWasMadeUnder()
    {
        var pairing = new Guid("11111111-2222-3333-4444-555555555555");

        var request = RequestFor(Observed("Overview", local: null, peer: "theirs"));
        var plan = Planner.Plan(new PlanRequest
        {
            PairingId = pairing,
            Direction = request.Direction,
        });

        Assert.Equal(pairing, plan.PairingId);
        Assert.Equal(SyncDirection.TwoWay, plan.Direction);
        Assert.Empty(plan.Items);
    }

    /// <summary>
    /// A request with no items is a plan with no items rather than a refusal.
    /// A pass that found nothing to consider is a normal event.
    /// </summary>
    [Fact]
    public void AnEmptyRequestIsAnEmptyPlan()
    {
        var plan = Planner.Plan(new PlanRequest());

        Assert.Empty(plan.Items);
        Assert.Equal(0, plan.FieldsConsidered);
        Assert.Equal(0, plan.FieldsToWrite);
    }

    /// <summary>
    /// Planning nothing is refused rather than treated as planning an empty
    /// pass, because the two are different mistakes and only one of them is a
    /// caller with nothing to do.
    /// </summary>
    [Fact]
    public void PlanningFromARequestThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => Planner.Plan(null!));
    }

    private static FieldObservation Observed(string field, string? local, string? peer)
    {
        return new FieldObservation
        {
            Field = field,
            LocalValue = local,
            PeerValue = peer,
            LastWrittenByThisPlugin = null,
            FieldLockedHere = false,
            FieldLockedOnPeer = false,
        };
    }

    private static FieldObservation FromInputs(ConflictInputs inputs)
    {
        return new FieldObservation
        {
            Field = "Overview",
            LocalValue = inputs.LocalValue,
            PeerValue = inputs.PeerValue,
            LastWrittenByThisPlugin = inputs.LastWrittenByThisPlugin,
            FieldLockedHere = inputs.FieldLockedHere,
            FieldLockedOnPeer = inputs.FieldLockedOnPeer,
        };
    }

    private static PlanRequest RequestFor(
        FieldObservation observation,
        string kind = AKindEveryGroupHolds,
        bool itemLocked = false)
    {
        var item = new ItemObservation
        {
            LocalItemId = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
            PeerItemId = new Guid("bbbbbbbb-0000-0000-0000-000000000002"),
            Kind = kind,
            ItemLockedHere = itemLocked,
        };

        item.Fields.Add(observation);

        var request = new PlanRequest { Direction = SyncDirection.TwoWay };
        request.Items.Add(item);
        return request;
    }

    /// <summary>
    /// Collects the types reachable from a type through its public properties,
    /// following generic arguments and stopping at anything already seen.
    /// </summary>
    private static void Walk(Type type, HashSet<Type> seen)
    {
        if (!seen.Add(type))
        {
            return;
        }

        foreach (var argument in type.GetGenericArguments())
        {
            Walk(argument, seen);
        }

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            Walk(underlying, seen);
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            Walk(property.PropertyType, seen);
        }
    }

    private static bool IsAServerType(Type type)
    {
        var name = type.FullName ?? type.Name;
        return name.StartsWith("MediaBrowser.", StringComparison.Ordinal)
            || (name.StartsWith("Jellyfin.", StringComparison.Ordinal)
                && !name.StartsWith("Jellyfin.Plugin.MetadataSync.", StringComparison.Ordinal));
    }

    private static bool IsADatabaseType(Type type)
    {
        var name = type.FullName ?? type.Name;
        return name.StartsWith("System.Data", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || name.StartsWith("System.IO", StringComparison.Ordinal);
    }

    private static bool IsATransportType(Type type)
    {
        var name = type.FullName ?? type.Name;
        return name.StartsWith("System.Net", StringComparison.Ordinal);
    }

    private static bool IsAClock(Type type)
    {
        return type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeProvider)
            || string.Equals(type.Name, "TimeProvider", StringComparison.Ordinal);
    }
}
