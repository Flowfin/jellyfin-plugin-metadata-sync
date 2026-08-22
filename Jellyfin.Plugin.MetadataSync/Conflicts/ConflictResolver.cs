using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Jellyfin.Plugin.MetadataSync.Conflicts;

/// <summary>
/// Decides what happens to one field whose two servers disagree, by walking the
/// declared rules in the order they are declared in and stopping at the first
/// one that fires.
/// </summary>
/// <remarks>
/// It is a function and it is nothing else. It takes
/// <see cref="ConflictInputs"/> and returns a <see cref="ConflictDecision"/>,
/// it holds no state, its constructor takes nothing, and there is no interface
/// anywhere in what it exposes. So there is nothing to substitute in a test and
/// nothing that could make the same inputs answer differently twice: the whole
/// rule set is exercised from a table with no server, no network and no clock
/// in the room.
/// <para>
/// The order is data. This type carries one condition per rule and never a
/// sequence of them, and the sequence is read out of the rule table each time,
/// so moving a row in <c>conflict-rules.json</c> changes what this resolver
/// does. That is the property the document claims when it says the order is
/// part of the declaration, and a resolver with the order written into its own
/// control flow would make that sentence false without changing a character of
/// it.
/// </para>
/// <para>
/// Every condition is written to stand on its own rather than to lean on the
/// rules above it having been asked first. The table's conditions are short
/// because they are read in order, and a condition that quietly assumed its
/// position would answer differently after a reorder while the row it renders
/// from said the same thing.
/// </para>
/// <para>
/// What this type does not do: it does not read the register, so a field that
/// may not move never reaches it; it does not record anything, which is #48;
/// and it does not write, which is <see cref="Reconciliation.LibraryPlanTarget"/>
/// by way of the plan this answer ends up on.
/// </para>
/// </remarks>
public sealed class ConflictResolver
{
    /// <summary>
    /// One condition per declared rule, keyed by the name the rule is declared
    /// under. The suite holds this set against the rule table in both
    /// directions, so a rule with no condition and a condition naming no rule
    /// are both refused before either can ship.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<ConflictInputs, bool>> _conditions =
        new ReadOnlyDictionary<string, Func<ConflictInputs, bool>>(
            new Dictionary<string, Func<ConflictInputs, bool>>(StringComparer.Ordinal)
            {
                ["item-locked-here"] = static inputs => inputs.ItemLockedHere,

                ["field-locked-here"] = static inputs => inputs.FieldLockedHere,

                ["values-agree"] = ValuesAgree,

                ["peer-value-absent"] = static inputs => !HasValue(inputs.PeerValue) && HasValue(inputs.LocalValue),

                ["local-value-absent"] = static inputs => !HasValue(inputs.LocalValue) && HasValue(inputs.PeerValue),

                // Ordinal and exact. A comparison that ignored case or trimmed
                // would answer that an operator who edited nothing but the
                // capitalisation of a title had not edited it, and the whole
                // value of this row is that it separates an edit from an
                // update.
                ["local-unchanged-since-this-plugin-wrote-it"] = static inputs =>
                    !ValuesAgree(inputs)
                    && inputs.LastWrittenByThisPlugin is not null
                    && string.Equals(inputs.LocalValue, inputs.LastWrittenByThisPlugin, StringComparison.Ordinal),

                ["peer-field-locked"] = static inputs => !ValuesAgree(inputs) && inputs.FieldLockedOnPeer,
            });

    /// <summary>
    /// Decides one field against the rules that ship inside this assembly.
    /// </summary>
    /// <param name="inputs">What is known about the field on both servers.</param>
    /// <returns>The outcome, the rule that produced it, and the value this server is left holding.</returns>
    public ConflictDecision Resolve(ConflictInputs inputs) => Resolve(inputs, ConflictRules.Rules);

    /// <summary>
    /// Decides one field against a rule set handed in, which is how the suite
    /// runs the same resolver over a reordered table and over an empty one.
    /// </summary>
    /// <param name="inputs">What is known about the field on both servers.</param>
    /// <param name="rules">The rules to walk, in the order to walk them.</param>
    /// <returns>The outcome, the rule that produced it, and the value this server is left holding.</returns>
    /// <exception cref="InvalidOperationException">
    /// A rule in the set has no condition here, so nothing can say whether it
    /// fires.
    /// </exception>
    internal static ConflictDecision Resolve(ConflictInputs inputs, IReadOnlyList<ConflictRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        foreach (var rule in rules)
        {
            if (!_conditions.TryGetValue(rule.Id, out var fires))
            {
                throw new InvalidOperationException(NoConditionFor(rule));
            }

            if (fires(inputs))
            {
                return Decided(rule.Outcome, rule, inputs);
            }
        }

        // The table ran out. Nothing is written on either side and the
        // difference belongs to an operator, which is the floor the rule set
        // sits on rather than a row inside it. A default answer here is the one
        // change that would make every rule above it advisory.
        return Decided(ConflictOutcome.Refuse, rule: null, inputs);
    }

    /// <summary>
    /// Whether a value is a value at all. Whitespace is not, and that is a
    /// declaration in the document rather than an observation about the server.
    /// </summary>
    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Whether the two servers already say the same thing, with absence
    /// counting as one state and never repaired into another.
    /// </summary>
    private static bool ValuesAgree(ConflictInputs inputs)
    {
        if (!HasValue(inputs.LocalValue))
        {
            return !HasValue(inputs.PeerValue);
        }

        // Compared and never trimmed. Deciding that two values differing by a
        // trailing space are the same is a decision about the field, and it
        // belongs to whoever declares the field rather than to this table.
        return HasValue(inputs.PeerValue)
            && string.Equals(inputs.LocalValue, inputs.PeerValue, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the decision, and is the one place the outcome is turned into a
    /// value. Each arm hands back a value that arrived in the inputs, so the
    /// invariant that nothing is ever built out of the two is held by there
    /// being nowhere else for a value to come from.
    /// </summary>
    private static ConflictDecision Decided(ConflictOutcome outcome, ConflictRule? rule, ConflictInputs inputs)
    {
        return new ConflictDecision
        {
            Outcome = outcome,
            Rule = rule,
            Value = outcome == ConflictOutcome.TakePeer ? inputs.PeerValue : inputs.LocalValue,
        };
    }

    private static string NoConditionFor(ConflictRule rule) => string.Format(
        CultureInfo.InvariantCulture,
        "The rule '{0}' is declared and this resolver has no condition for it, so nothing says when it fires and no rule set carrying it can be evaluated.",
        rule.Id);
}
