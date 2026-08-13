using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Fields;
using Jellyfin.Plugin.MetadataSync.Matching;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The legs a coverage run asked for. Each one exists because the first run over
/// this plugin named a line or a branch in the decision code that nothing
/// reached, and each asserts what the code does at that branch rather than
/// visiting it to move a number.
///
/// They are collected in one file because they have one reason to exist rather
/// than one subject, and the reason is worth keeping visible: a branch nothing
/// reaches is a decision nothing exercises, and the alternative to reaching it is
/// deleting it. Where deleting was the better answer it is argued in the pull
/// request rather than settled quietly here.
/// </summary>
public class DecisionBranchTests
{
    /// <summary>
    /// The nine writers were reached by no test at all: the suite asserted which
    /// fields may move and every way a write is refused, and never that a write
    /// arrives. So the whole point of the type was the uncovered part.
    ///
    /// Driven off the register rather than off a list here, so a writer added
    /// later is asserted without anybody remembering to add it.
    /// </summary>
    [Fact]
    public void EveryFieldTheRegisterSaysMovesArrivesOnTheReceivingItem()
    {
        Assert.NotEmpty(FieldMover.WritableFields);

        foreach (var field in FieldMover.WritableFields)
        {
            var property = typeof(BaseItem).GetProperty(field);
            Assert.NotNull(property);

            var from = new Movie();
            var to = new Movie();
            var sent = ValueFor(property!.PropertyType, field);
            property.SetValue(from, sent);

            FieldMover.Move(field, from, to);

            Assert.Equal(sent, property.GetValue(to));
        }
    }

    /// <summary>
    /// A list-valued field is copied rather than shared, so the receiving item
    /// does not end up holding the array the sending item holds. The copy is what
    /// keeps a later edit on one server from appearing on the other inside one
    /// pass.
    /// </summary>
    [Fact]
    public void AListValuedFieldArrivesAsACopyAndNotAsTheSameArray()
    {
        var sent = new[] { "one", "two" };
        var from = new Movie { Tags = sent };
        var to = new Movie();

        FieldMover.Move("Tags", from, to);

        Assert.Equal(sent, to.Tags);
        Assert.NotSame(sent, to.Tags);
    }

    /// <summary>
    /// The absent case, which is the one the copy has a branch for. A server that
    /// holds no tags at all is not a server that holds an empty list, and the
    /// receiving item gets an empty list rather than a null the library would
    /// then carry.
    /// </summary>
    [Fact]
    public void AListValuedFieldThatIsAbsentArrivesAsAnEmptyList()
    {
        var from = new Movie { Tags = null! };
        var to = new Movie { Tags = new[] { "was here" } };

        FieldMover.Move("Tags", from, to);

        Assert.NotNull(to.Tags);
        Assert.Empty(to.Tags);
    }

    /// <summary>
    /// The field-level lock check has three parts and two of its arms were
    /// unreached. This is the arm where the register names no lock for the row at
    /// all: there is no lock to consult, so the write happens, and it happens
    /// without reading the item's lock list.
    /// </summary>
    [Fact]
    public void AFieldTheRegisterGovernsByNoLockIsWrittenEvenWhenOtherFieldsAreLocked()
    {
        var row = FieldRegister.RequireMovable("Tagline");
        Assert.Null(row.Lock);

        var from = new Movie { Tagline = "what the peer says" };
        var to = new Movie { Tagline = "ours", LockedFields = new[] { MetadataField.Name, MetadataField.Overview } };

        FieldMover.Move("Tagline", from, to);

        Assert.Equal("what the peer says", to.Tagline);
    }

    /// <summary>
    /// The other unreached arm: an item that carries no lock list at all. The
    /// server leaves it unset on an item nobody has locked anything on, which is
    /// the ordinary case, and reading it as an empty list rather than refusing is
    /// what makes the ordinary case work.
    /// </summary>
    [Fact]
    public void AGovernedFieldIsWrittenWhenTheItemCarriesNoLockListAtAll()
    {
        var row = FieldRegister.RequireMovable("Overview");
        Assert.NotNull(row.Lock);

        var from = new Movie { Overview = "What the peer says about it" };
        var to = new Movie { Overview = "ours", LockedFields = null! };

        FieldMover.Move("Overview", from, to);

        Assert.Equal("What the peer says about it", to.Overview);
    }

    /// <summary>
    /// The leading-zero normalisation had no test for the value that is nothing
    /// but zeros. Trimming every zero off leaves an empty string, which would
    /// compare equal to any other all-zero identifier and to nothing else, so the
    /// arm that puts one zero back is the difference between an identifier and an
    /// absence.
    /// </summary>
    [Theory]
    [InlineData("0000", "0")]
    [InlineData("0", "0")]
    [InlineData("0550", "550")]
    public void AnIdentifierOfNothingButZerosNormalisesToOneZero(string stored, string expected)
    {
        Assert.Equal(expected, ProviderIdentifiers.Normalise(stored, LeadingZeros(trimmed: true)));
    }

    /// <summary>
    /// The digit test is what decides whether the leading-zero rule may act, and
    /// it was never asked about a value that is not a number. Both directions
    /// outside the digit range were unreached, so both are here: a character
    /// below '0' and a character above '9'.
    ///
    /// An identifier that is not all digits is left exactly as it was stored,
    /// because trimming a zero off the front of an alphanumeric identifier would
    /// change it into a different identifier.
    /// </summary>
    [Theory]
    [InlineData("0tt1375666")]
    [InlineData("0-550")]
    [InlineData("0/550")]
    [InlineData("05.5")]
    public void AnIdentifierThatIsNotAllDigitsKeepsItsLeadingZeros(string stored)
    {
        Assert.Equal(stored, ProviderIdentifiers.Normalise(stored, LeadingZeros(trimmed: true)));
    }

    /// <summary>
    /// An empty identifier is not a number either, and the digit test answers
    /// that before it looks at any character. Without the arm, an empty value
    /// would be normalised to one zero and would then compare equal to a real
    /// identifier of zero.
    /// </summary>
    [Fact]
    public void AnEmptyIdentifierIsNotTreatedAsANumber()
    {
        Assert.Equal(string.Empty, ProviderIdentifiers.Normalise(string.Empty, LeadingZeros(trimmed: true)));
        Assert.Equal(string.Empty, ProviderIdentifiers.Normalise(null, LeadingZeros(trimmed: true)));
    }

    /// <summary>
    /// A rule that does not trim leaves the whitespace in place, and no test had
    /// ever handed the normaliser one. The row's own column is what decides it,
    /// so a rule set that turns trimming off for a provider whose identifiers are
    /// meaningfully spaced keeps working.
    /// </summary>
    [Fact]
    public void ARuleThatDoesNotTrimLeavesTheWhitespaceWhereItWas()
    {
        var untrimmed = new ProviderIdentifierRule(
            "Fixture",
            "Ordinal",
            trimmed: false,
            "none",
            "A fixture rule, so the normaliser is asked what it does when the row says do not trim.");

        Assert.Equal("  550  ", ProviderIdentifiers.Normalise("  550  ", untrimmed));
    }

    /// <summary>
    /// The table says how a provider name is compared and why, and the sentence
    /// was read by nothing. A reason nobody reads is a reason nobody maintains,
    /// and the sentence is the whole argument for comparing keys the way the
    /// table does.
    /// </summary>
    [Fact]
    public void TheTableSaysWhyAProviderNameIsComparedTheWayItIs()
    {
        var table = ProviderIdentifiers.Load(ProviderIdentifiers.EmbeddedResourceName);

        Assert.False(string.IsNullOrWhiteSpace(table.KeyComparison));
        Assert.True(
            table.KeyComparisonReason.Length > 40,
            "The table's key comparison carries no argued reason: '" + table.KeyComparisonReason + "'.");
    }

    /// <summary>
    /// A rule needs a condition and a reason, and the loader refuses a row that is
    /// missing either. One of the two arms had never been reached, so a table
    /// could have been written that the loader was only half holding.
    ///
    /// A rule with no reason is a rule nobody can disagree with later, and a rule
    /// with no condition is one nobody can predict, which is what the declared
    /// rule set exists against in both directions.
    /// </summary>
    [Theory]
    [InlineData("   ", "The absent value is absence of information rather than information.")]
    [InlineData("The peer has no value for this field and this server has one.", "   ")]
    public void ARuleMissingItsConditionOrItsReasonIsRefused(string condition, string reason)
    {
        var refused = Assert.Throws<InvalidOperationException>(() => ConflictRules.Parse(
            """
            {
              "rules": [
                {
                  "id": "peer-value-absent",
                  "condition": "CONDITION",
                  "outcome": "KeepLocal",
                  "reason": "REASON"
                }
              ]
            }
            """
                .Replace("CONDITION", condition, StringComparison.Ordinal)
                .Replace("REASON", reason, StringComparison.Ordinal)));

        Assert.Contains("peer-value-absent", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two of the declared rules start by asking whether the two values differ,
    /// and neither had ever been asked with values that agree. Reached that way
    /// each has to decline: two equal values are not a local edit and are not a
    /// disagreement the peer's lock decides either.
    ///
    /// They are reached by handing the resolver one rule and nothing else, which is
    /// how the suite already runs a reordered table. In the declared order the
    /// agreement rule fires above both, so the branch is unreachable through
    /// <see cref="ConflictResolver.Resolve(ConflictInputs)"/> and is not
    /// unreachable in the rule. That is the argument for reaching it rather than
    /// deleting it: the guard is what keeps the rule correct if the order changes,
    /// and the order is data.
    /// </summary>
    /// <param name="id">The rule to run alone.</param>
    [Theory]
    [InlineData("local-unchanged-since-this-plugin-wrote-it")]
    [InlineData("peer-field-locked")]
    public void ARuleThatReadsADifferenceDeclinesWhenTheTwoValuesAgree(string id)
    {
        var justThatRule = ConflictRules.Rules
            .Where(rule => string.Equals(rule.Id, id, StringComparison.Ordinal))
            .ToList();

        Assert.Single(justThatRule);

        var decision = ConflictResolver.Resolve(
            ConflictResolverTests.NothingOnEitherSide() with
            {
                LocalValue = "the same text",
                PeerValue = "the same text",
                LastWrittenByThisPlugin = "the same text",
                FieldLockedOnPeer = true,
            },
            justThatRule);

        Assert.Equal(ConflictOutcome.Refuse, decision.Outcome);
        Assert.Null(decision.Rule);
    }

    /// <summary>
    /// Both refusals the mover raises are exception types with the three
    /// constructors the analyzer's exception pattern asks for, and two of the
    /// three were constructed by nothing.
    ///
    /// Deleting them was the other answer and it was not taken: an exception type
    /// that cannot carry an inner exception is one a later caller cannot wrap when
    /// it fails while refusing, and the parameterless form is what says what the
    /// type means with no context to hand. So they are asserted to say something
    /// rather than removed.
    /// </summary>
    [Fact]
    public void ARefusalSaysWhatItIsAboutWithNoContextAndCarriesOneWhenThereIs()
    {
        var cause = new InvalidOperationException("the register could not be read");

        Assert.Contains("locked", new FieldLockedException().Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(cause, new FieldLockedException("while refusing a write", cause).InnerException);

        Assert.Contains("register", new FieldNotDeclaredException().Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(cause, new FieldNotDeclaredException("while refusing a write", cause).InnerException);

        Assert.Contains("value", new WriteRefusedException().Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(cause, new WriteRefusedException("while writing an item", cause).InnerException);

        Assert.Contains("library", new ItemNotInLibraryException().Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(cause, new ItemNotInLibraryException("while fetching an item", cause).InnerException);
    }

    private static ProviderIdentifierRule LeadingZeros(bool trimmed) => new(
        "Fixture",
        "Ordinal",
        trimmed,
        "LeadingZeros",
        "A fixture rule, so the normaliser is asked about a value the real table's providers do not produce.");

    /// <summary>
    /// A value of the property's own type, distinctive enough that a write to the
    /// wrong field would fail the assertion rather than pass by coincidence.
    /// </summary>
    private static object ValueFor(Type propertyType, string field)
    {
        var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (underlying == typeof(string))
        {
            return "what the peer holds for " + field;
        }

        if (underlying == typeof(string[]))
        {
            return new[] { field + " one", field + " two" };
        }

        if (underlying == typeof(DateTime))
        {
            return new DateTime(1977, 5, 25, 0, 0, 0, DateTimeKind.Utc);
        }

        if (underlying == typeof(int))
        {
            return 1977;
        }

        throw new NotSupportedException(string.Format(
            CultureInfo.InvariantCulture,
            "The register declares '{0}' as movable and this leg has no value of type {1} to send. Add one here rather than dropping the field from the walk.",
            field,
            propertyType));
    }
}
