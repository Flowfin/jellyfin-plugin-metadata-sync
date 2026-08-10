using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The resolver, run against every case the document argues the rule set from.
/// </summary>
/// <remarks>
/// This is the file that turns <c>docs/conflicts.md</c> from a design into a
/// statement about behaviour. <see cref="ConflictRuleTests"/> holds the document
/// and the declared table against each other and can say nothing about what
/// happens to a field; every test here hands the resolver a row out of that same
/// document and compares what comes back.
/// <para>
/// Nothing is substituted anywhere below, and there is nothing to substitute.
/// The resolver takes plain values and returns plain values, so a case is
/// arranged by writing down what two servers hold.
/// </para>
/// </remarks>
public class ConflictResolverTests
{
    /// <summary>
    /// A rule table carrying a rule this resolver has no condition for. It is
    /// the mistake somebody makes by adding a row to the declared table and
    /// stopping there, and the outcome it names is a legal one, so nothing
    /// earlier in the read refuses it on the way past. Used by
    /// <see cref="RefusalTests"/> to reach that refusal.
    /// </summary>
    internal const string RuleWithNoConditionHere = """
        { "rules": [ { "id": "peer-value-longer", "condition": "The peer's value is longer than this server's.", "outcome": "TakePeer", "reason": "A longer overview is usually the fuller one." } ] }
        """;

    /// <summary>
    /// Gets the name of every case in the document's fixture table.
    /// </summary>
    public static TheoryData<string> CaseNames => ConflictFixtures.CaseNames;

    /// <summary>
    /// Every case is answered with the outcome the document declares for it.
    /// This is the whole point of the issue that asked for the rules to be
    /// written before the resolver: the expected answers were fixed by a
    /// document nobody could tune against an implementation, because the
    /// implementation did not exist.
    /// </summary>
    /// <param name="caseName">The case, named by the document.</param>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public void EveryCaseIsAnsweredWithTheOutcomeTheDocumentDeclares(string caseName)
    {
        var row = ConflictFixtures.Named(caseName);

        var decision = new ConflictResolver().Resolve(ConflictFixtures.InputsFor(row));

        Assert.Equal(ConflictFixtures.OutcomeFor(row), decision.Outcome);
    }

    /// <summary>
    /// Every case is answered by the rule the document names, and a case the
    /// document says no rule answers comes back naming none.
    /// </summary>
    /// <remarks>
    /// The outcome alone would pass this suite with the wrong rule firing,
    /// because three rules produce <see cref="ConflictOutcome.KeepLocal"/> and
    /// two produce <see cref="ConflictOutcome.TakePeer"/>. Four rows in the
    /// table exist to separate exactly those, and they only separate anything
    /// if the name is compared as well.
    /// </remarks>
    /// <param name="caseName">The case, named by the document.</param>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public void EveryCaseIsAnsweredByTheRuleTheDocumentNames(string caseName)
    {
        var row = ConflictFixtures.Named(caseName);

        var decision = new ConflictResolver().Resolve(ConflictFixtures.InputsFor(row));

        Assert.Equal(ConflictFixtures.RuleIdFor(row), decision.Rule?.Id);
    }

    /// <summary>
    /// A case no declared rule answers refuses, and says so by naming no rule
    /// rather than by naming one that did not really fire.
    /// </summary>
    [Fact]
    public void ACaseNoRuleAnswersRefusesAndNamesNoRule()
    {
        var residual = ConflictFixtures.Rows()
            .Where(row => ConflictFixtures.RuleIdFor(row) is null)
            .ToList();

        Assert.NotEmpty(residual);

        foreach (var row in residual)
        {
            var decision = new ConflictResolver().Resolve(ConflictFixtures.InputsFor(row));

            Assert.Null(decision.Rule);
            Assert.Equal(ConflictOutcome.Refuse, decision.Outcome);
        }
    }

    /// <summary>
    /// The same inputs answer the same way twice. Run over the whole table,
    /// because a resolver that reached for a clock, a random tiebreak or a
    /// mutable field would answer one row differently and every other row the
    /// same.
    /// </summary>
    [Fact]
    public void TheResolverIsDeterministicOverTheWholeFixtureTable()
    {
        var resolver = new ConflictResolver();

        var first = ConflictFixtures.Rows().Select(row => resolver.Resolve(ConflictFixtures.InputsFor(row))).ToList();
        var second = ConflictFixtures.Rows().Select(row => resolver.Resolve(ConflictFixtures.InputsFor(row))).ToList();

        Assert.Equal(first, second);
    }

    /// <summary>
    /// The resolver never returns a value that is not one of the two it was
    /// given. Held by reference rather than by equality, so a value rebuilt out
    /// of the inputs fails here even where it compares equal to one of them.
    /// </summary>
    /// <remarks>
    /// This is the invariant the closed outcome set exists to protect. A merged
    /// overview and a union of two genre sets both arrive as a value neither
    /// operator wrote, and they arrive without anybody adding an outcome: they
    /// arrive as one line inside a rule that thought it was being helpful.
    /// </remarks>
    [Fact]
    public void TheResolverNeverReturnsAValueThatIsNotOneOfItsInputs()
    {
        var resolver = new ConflictResolver();

        foreach (var row in ConflictFixtures.Rows())
        {
            var inputs = ConflictFixtures.InputsFor(row);

            var value = resolver.Resolve(inputs).Value;

            Assert.True(
                ReferenceEquals(value, inputs.LocalValue) || ReferenceEquals(value, inputs.PeerValue),
                row[0] + " came back with a value that is neither server's.");
        }
    }

    /// <summary>
    /// The resolver is constructed with nothing and holds nothing. A
    /// constructor parameter is where a substitute gets in, and a field is
    /// where an answer that depends on the last call gets in.
    /// </summary>
    [Fact]
    public void TheResolverIsConstructedWithNothingAndHoldsNothing()
    {
        var type = typeof(ConflictResolver);

        var constructors = type.GetConstructors();
        Assert.Single(constructors);
        Assert.Empty(constructors[0].GetParameters());

        Assert.Empty(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

        Assert.NotNull(new ConflictResolver());
    }

    /// <summary>
    /// Nothing the resolver exposes names an interface, on the way in or on the
    /// way out. An interface in the signature is how a resolver acquires a
    /// dependency it can be handed a substitute for, and a rule set that can be
    /// handed a substitute is one whose fixtures prove less than they look like
    /// they prove.
    /// </summary>
    [Fact]
    public void NothingTheResolverExposesNamesAnInterface()
    {
        Type[] surface = [typeof(ConflictResolver), typeof(ConflictInputs), typeof(ConflictDecision)];

        foreach (var type in surface)
        {
            foreach (var constructor in type.GetConstructors())
            {
                Assert.All(constructor.GetParameters(), p => Assert.False(p.ParameterType.IsInterface, type.Name + " is constructed from " + p.ParameterType.Name));
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                Assert.False(method.ReturnType.IsInterface, type.Name + "." + method.Name + " returns " + method.ReturnType.Name);
                Assert.All(method.GetParameters(), p => Assert.False(p.ParameterType.IsInterface, type.Name + "." + method.Name + " takes " + p.ParameterType.Name));
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                Assert.False(property.PropertyType.IsInterface, type.Name + "." + property.Name + " is a " + property.PropertyType.Name);
            }
        }
    }

    /// <summary>
    /// Every declared rule has a condition here, and every condition here names
    /// a declared rule. Both directions, because a rule with no condition
    /// cannot be evaluated and a condition with no rule is dead code that reads
    /// like a rule somebody can rely on.
    /// </summary>
    [Fact]
    public void TheConditionsAndTheDeclaredRulesAreTheSameSet()
    {
        var declared = ConflictRules.Rules.Select(rule => rule.Id).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(declared, ConditionNames());
    }

    /// <summary>
    /// The order the rules are evaluated in comes from the table and not from
    /// this resolver.
    /// </summary>
    /// <remarks>
    /// The pair moved here is the one the document says proves it. A peer that
    /// holds the field locked while this server holds nothing takes the peer's
    /// value, because there is nothing here to defend; move the lock rule above
    /// the absence rule and the same inputs refuse instead. Nothing about the
    /// resolver changes between the two calls, so the answer moved because the
    /// declaration did.
    /// </remarks>
    [Fact]
    public void TheEvaluationOrderComesFromTheDeclaredTable()
    {
        var inputs = ConflictFixtures.InputsFor(
            ConflictFixtures.Named("the peer holds this field locked and this server has nothing"));

        var asDeclared = new ConflictResolver().Resolve(inputs);
        Assert.Equal(ConflictOutcome.TakePeer, asDeclared.Outcome);
        Assert.Equal("local-value-absent", asDeclared.Rule?.Id);

        var reordered = ConflictResolver.Resolve(inputs, LockRuleMovedAbove("local-value-absent"));

        Assert.Equal(ConflictOutcome.Refuse, reordered.Outcome);
        Assert.Equal("peer-field-locked", reordered.Rule?.Id);
    }

    /// <summary>
    /// A rule set carrying a rule this resolver has no condition for is refused
    /// rather than walked past. Walking past it would evaluate a rule set that
    /// is not the one that was declared, and the row skipped would be invisible
    /// in the answer.
    /// </summary>
    [Fact]
    public void ARuleWithNoConditionIsRefused()
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => ConflictResolver.Resolve(NothingOnEitherSide(), ConflictRules.Parse(RuleWithNoConditionHere)));

        Assert.Contains("peer-value-longer", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// No rule set at all is refused rather than read as an empty one.
    /// </summary>
    [Fact]
    public void NoRuleSetIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ConflictResolver.Resolve(NothingOnEitherSide(), rules: null!));
    }

    /// <summary>
    /// Inputs for a field neither server has a value for, used where what is
    /// being asserted is the refusal in front of the walk rather than an
    /// outcome. Shared with <see cref="RefusalTests"/>, which reaches the same
    /// two sites from its register.
    /// </summary>
    /// <returns>Inputs that reach no rule.</returns>
    internal static ConflictInputs NothingOnEitherSide() => new()
    {
        LocalValue = null,
        PeerValue = null,
        LastWrittenByThisPlugin = null,
        ItemLockedHere = false,
        FieldLockedHere = false,
        FieldLockedOnPeer = false,
    };

    /// <summary>
    /// The declared rules with the peer's lock rule lifted above the rule
    /// named, and nothing else changed.
    /// </summary>
    private static IReadOnlyList<ConflictRule> LockRuleMovedAbove(string id)
    {
        var reordered = ConflictRules.Rules.ToList();

        var lockRule = reordered.Single(rule => string.Equals(rule.Id, "peer-field-locked", StringComparison.Ordinal));
        reordered.Remove(lockRule);
        reordered.Insert(reordered.FindIndex(rule => string.Equals(rule.Id, id, StringComparison.Ordinal)), lockRule);

        return reordered;
    }

    /// <summary>
    /// Reads back the names the resolver carries a condition for. The table is
    /// private, because a rule set anybody can add a condition to from outside
    /// is not a rule set, so the names are read off the field the same way a
    /// reader would find them.
    /// </summary>
    private static IReadOnlyList<string> ConditionNames()
    {
        var field = typeof(ConflictResolver).GetField("_conditions", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var conditions = field.GetValue(null) as IReadOnlyDictionary<string, Func<ConflictInputs, bool>>;
        Assert.NotNull(conditions);

        return conditions.Keys.Order(StringComparer.Ordinal).ToList();
    }
}
