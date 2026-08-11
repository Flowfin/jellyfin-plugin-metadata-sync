using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// One obligation per declared conflict rule: the rule is why its case is
/// answered the way it is, and taking that one rule out changes the answer.
/// </summary>
/// <remarks>
/// <see cref="ConflictResolverTests"/> runs every case the document argues the
/// rule set from and compares the outcome and the rule name that come back.
/// That says a rule answered a case. It does not say the rule was needed, and
/// for four of the seven rules it could not: three rules produce
/// <see cref="ConflictOutcome.KeepLocal"/> and two produce
/// <see cref="ConflictOutcome.TakePeer"/>, so a row that never fired would leave
/// its cases to a row underneath that produces the same outcome, and every
/// fixture would stay green.
/// <para>
/// <see cref="ConflictFloorTests"/> takes the whole table away, which proves
/// there is no answer underneath it. That is the opposite end of the same
/// question and it says nothing about any individual rule: with nothing
/// declared, every rule is equally absent.
/// </para>
/// <para>
/// So each rule is removed on its own, with the other six left in the order
/// they are declared in, and the document declares what the same case answers
/// once it is gone. The declared answer is what makes this more than a
/// tautology: with the rule removed nothing can come back under its name, so
/// comparing names would prove only that the rule was deleted. What is compared
/// is the outcome and the rule that takes over, which is a prediction about the
/// six remaining rows.
/// </para>
/// <para>
/// The bound is worth having in writing. What is held here is that every rule
/// the table declares is needed by a case. What cannot be held here is that a
/// rule which used to be declared still is: a rule removed from
/// <c>conflict-rules.json</c> takes its obligation here away with it, so the
/// set this file walks is the set that exists rather than the set that was
/// intended. What stands in the way of that is the reader of the diff and
/// nothing in the suite.
/// </para>
/// </remarks>
public class ConflictRuleBiteTests
{
    private const string BiteHeader = "| Rule | Proved on | Rule once it is gone | Outcome once it is gone |";

    /// <summary>
    /// Gets the name of every declared rule, so a case in the theory below is
    /// named by the rule it is about and an eighth rule arrives with an
    /// obligation rather than without one.
    /// </summary>
    public static TheoryData<string> RuleIds
    {
        get
        {
            var ids = new TheoryData<string>();
            foreach (var rule in ConflictRules.Rules)
            {
                ids.Add(rule.Id);
            }

            return ids;
        }
    }

    /// <summary>
    /// Every declared rule is proved on a case, and the case answers what the
    /// document declares once that rule alone is taken out of the table.
    /// </summary>
    /// <remarks>
    /// This is the test that fails when a rule stops firing. A condition edited
    /// so that it never fires reds the case with the whole table present; a
    /// condition edited so that it always fires reds the neighbouring rules'
    /// cases, because it takes them over.
    /// </remarks>
    /// <param name="ruleId">The rule, named as the table declares it.</param>
    [Theory]
    [MemberData(nameof(RuleIds))]
    public void EveryDeclaredRuleIsWhyItsOwnCaseIsAnsweredAsItIs(string ruleId)
    {
        var row = BiteRowFor(ruleId);
        var inputs = ConflictFixtures.InputsFor(ConflictFixtures.Named(row[1]));

        var asDeclared = new ConflictResolver().Resolve(inputs);
        Assert.Equal(ruleId, asDeclared.Rule?.Id);

        var withoutIt = ConflictResolver.Resolve(inputs, EveryRuleBut(ruleId));

        Assert.Equal(OutcomeIn(row[3]), withoutIt.Outcome);
        Assert.Equal(RuleIn(row[2]), withoutIt.Rule?.Id);
    }

    /// <summary>
    /// The answer with the rule gone is not the answer with it there. Both
    /// halves of the pair are read off the document, so a row declaring the
    /// answer it already had would be a row proving nothing, and it is refused
    /// here rather than passing quietly.
    /// </summary>
    /// <remarks>
    /// The pair is compared and not only the outcome. One rule refuses either
    /// way and is held up by the name an operator is told rather than by the
    /// value, which the document argues under the table. A check written on the
    /// outcome alone would call that rule dead and a reader following it would
    /// delete it.
    /// </remarks>
    [Fact]
    public void NoRowDeclaresTheAnswerItAlreadyHad()
    {
        foreach (var row in BiteRows())
        {
            var declared = ConflictFixtures.Named(row[1]);

            var moved = OutcomeIn(row[3]) != ConflictFixtures.OutcomeFor(declared)
                || !string.Equals(RuleIn(row[2]), ConflictFixtures.RuleIdFor(declared), StringComparison.Ordinal);

            Assert.True(moved, row[0] + " declares the answer it already had, so the case does not prove the rule.");
        }
    }

    /// <summary>
    /// Every declared rule has a row here and every row names a declared rule.
    /// Both directions, because a rule with no row is a rule nothing proves is
    /// needed, and a row naming no rule is a proof about something that is not
    /// in the table any more.
    /// </summary>
    [Fact]
    public void TheRowsAndTheDeclaredRulesAreTheSameSet()
    {
        var declared = ConflictRules.Rules.Select(rule => rule.Id).Order(StringComparer.Ordinal).ToList();

        var proved = BiteRows()
            .Select(row => ConflictFixtures.Unquote(row[0]))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(declared, proved);
    }

    /// <summary>
    /// Every row is proved on a case the fixture table says that rule answers.
    /// A row free to name any case at all could pick one its rule never touches,
    /// and the answer would then move for a reason that has nothing to do with
    /// the rule being removed.
    /// </summary>
    [Fact]
    public void EveryRowIsProvedOnACaseItsOwnRuleAnswers()
    {
        foreach (var row in BiteRows())
        {
            var ruleId = ConflictFixtures.Unquote(row[0]);

            Assert.Equal(ruleId, ConflictFixtures.RuleIdFor(ConflictFixtures.Named(row[1])));
        }
    }

    /// <summary>
    /// A rule the document declares as taking over is one the table declares.
    /// The residual is spelled as an empty cell and is not a rule, so a name
    /// that is neither is a typo that would otherwise read as a rule somebody
    /// can look up.
    /// </summary>
    [Fact]
    public void EveryRuleNamedAsTakingOverIsADeclaredRule()
    {
        foreach (var row in BiteRows())
        {
            var successor = RuleIn(row[2]);
            if (successor is null)
            {
                continue;
            }

            Assert.NotNull(ConflictRules.Find(successor));
            Assert.NotEqual(ConflictFixtures.Unquote(row[0]), successor);
        }
    }

    /// <summary>
    /// A rule removed on its own leaves the other six in the order they were
    /// declared in. The removal is what is being measured, so a reorder
    /// underneath it would answer a different question with the same
    /// assertions.
    /// </summary>
    [Fact]
    public void RemovingOneRuleLeavesTheOthersInTheDeclaredOrder()
    {
        foreach (var rule in ConflictRules.Rules)
        {
            var expected = ConflictRules.Rules.Where(r => r != rule).Select(r => r.Id).ToList();

            Assert.Equal(expected, EveryRuleBut(rule.Id).Select(r => r.Id).ToList());
        }
    }

    /// <summary>
    /// The declared table with one rule taken out of it and nothing else
    /// changed.
    /// </summary>
    /// <param name="id">The rule to remove.</param>
    /// <returns>The remaining rules, in the order they are declared in.</returns>
    private static IReadOnlyList<ConflictRule> EveryRuleBut(string id)
    {
        var kept = ConflictRules.Rules
            .Where(rule => !string.Equals(rule.Id, id, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(ConflictRules.Rules.Count - 1, kept.Count);
        return kept;
    }

    /// <summary>
    /// The rows of the table that declares what each rule is holding up.
    /// </summary>
    /// <returns>The rows, each split into its four cells.</returns>
    private static IReadOnlyList<string[]> BiteRows() => ConflictFixtures.RowsUnder(BiteHeader, 4);

    /// <summary>
    /// The row that declares what one rule is holding up.
    /// </summary>
    /// <param name="ruleId">The rule.</param>
    /// <returns>The row.</returns>
    private static string[] BiteRowFor(string ruleId)
    {
        var row = BiteRows().SingleOrDefault(
            r => string.Equals(ConflictFixtures.Unquote(r[0]), ruleId, StringComparison.Ordinal));

        Assert.NotNull(row);
        return row;
    }

    /// <summary>
    /// The rule a cell names, or null where the cell is empty and the case
    /// falls to the refusal.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <returns>The rule name, or null for the residual.</returns>
    private static string? RuleIn(string cell) => cell.Length == 0 ? null : ConflictFixtures.Unquote(cell);

    /// <summary>
    /// The outcome a cell names.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <returns>The outcome.</returns>
    private static ConflictOutcome OutcomeIn(string cell)
    {
        var name = ConflictFixtures.Unquote(cell);

        Assert.Contains(name, Enum.GetNames<ConflictOutcome>(), StringComparer.Ordinal);
        return Enum.Parse<ConflictOutcome>(name, ignoreCase: false);
    }
}
