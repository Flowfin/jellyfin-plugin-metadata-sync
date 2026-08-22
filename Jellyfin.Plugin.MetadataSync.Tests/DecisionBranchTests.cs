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
    /// The refusals this plugin raises are exception types with the three
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

        Assert.Contains("register", new FieldNotDeclaredException().Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(cause, new FieldNotDeclaredException("while refusing a write", cause).InnerException);

        Assert.Contains("value", new WriteRefusedException().Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(cause, new WriteRefusedException("while writing an item", cause).InnerException);

        Assert.Contains("library", new ItemNotInLibraryException().Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(cause, new ItemNotInLibraryException("while fetching an item", cause).InnerException);

        Assert.Contains("wrote the item", new ItemChangedSincePlannedException().Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(cause, new ItemChangedSincePlannedException("while comparing a token", cause).InnerException);
    }

    private static ProviderIdentifierRule LeadingZeros(bool trimmed) => new(
        "Fixture",
        "Ordinal",
        trimmed,
        "LeadingZeros",
        "A fixture rule, so the normaliser is asked about a value the real table's providers do not produce.");
}
