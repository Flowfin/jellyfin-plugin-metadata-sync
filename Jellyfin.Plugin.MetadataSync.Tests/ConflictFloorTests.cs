using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// There is no answer underneath the declared rules. A difference the table
/// does not reach is refused, and taking the rules away leaves refusal rather
/// than a default that was there all along.
/// </summary>
/// <remarks>
/// <see cref="ConflictResolverTests"/> runs the declared table over the cases
/// the document argues it from, so every one of them is answered by a rule and
/// the floor is reached by three rows. That says nothing about what is
/// underneath the table, because a resolver holding a default would still pass
/// every one of those rows: the default would sit below seven rules that answer
/// first.
/// <para>
/// So the table is taken away instead. What is left is the resolver with
/// nothing to walk, and every arrangement it can be handed has to come back
/// refused and naming no rule. A default of any kind shows up here as the one
/// place it cannot hide, and it shows up for every arrangement rather than for
/// the ones somebody thought to write down.
/// </para>
/// <para>
/// The document's fixture table describes arrangements and never fields,
/// because <see cref="ConflictInputs"/> carries no field and no item. That is
/// why a sweep over the input surface is the same statement as one over the
/// fields: there is nothing a rule could read that would tell one field from
/// another, so an arrangement that refuses refuses for every field it could
/// have come from.
/// </para>
/// </remarks>
public class ConflictFloorTests
{
    /// <summary>
    /// The values this server can hold, as distinct instances so that the value
    /// coming back can be compared by reference rather than by equality. Two of
    /// them are the spellings of absence the rules read as one, and the last
    /// two differ from each other so that a swap is visible.
    /// </summary>
    private static readonly string?[] _localValues = [null, string.Empty, " ", "Kept here", "Kept here as well"];

    /// <summary>
    /// The values the peer can hold. Written differently from the local ones on
    /// purpose: a resolver handing back the peer's value where it should hand
    /// back this server's is only visible where the two are different objects.
    /// </summary>
    private static readonly string?[] _peerValues = [null, string.Empty, " ", "From the peer", "From the peer as well"];

    /// <summary>
    /// What this plugin may have written for the field before. The two that are
    /// not null are the same instances the local and peer sets carry, so the
    /// sweep reaches the arrangement where this server still holds exactly what
    /// this plugin wrote, which is the one rule 6 turns on.
    /// </summary>
    private static readonly string?[] _lastWritten = [null, "Kept here", "From the peer"];

    /// <summary>
    /// A rule set with nothing in it. It is a legal rule set rather than a
    /// missing one: handing over no rules at all is refused as a mistake by the
    /// resolver, and this is the table that ran out.
    /// </summary>
    private static readonly IReadOnlyList<ConflictRule> _nothingIsDeclared = Array.Empty<ConflictRule>();

    /// <summary>
    /// Gets the name of every case in the document's fixture table.
    /// </summary>
    public static TheoryData<string> CaseNames => ConflictFixtures.CaseNames;

    /// <summary>
    /// Every case the document argues the rule set from refuses once the rules
    /// are taken away, including the eleven the declared table answers with a
    /// rule. Three of the fourteen refuse either way and the other eleven are
    /// the ones that carry this: each of them is answered by a rule today, so a
    /// default underneath would be invisible until the rule above it went away.
    /// </summary>
    /// <param name="caseName">The case, named by the document.</param>
    [Theory]
    [MemberData(nameof(CaseNames))]
    public void EveryCaseTheDocumentArguesRefusesOnceTheRulesAreTakenAway(string caseName)
    {
        var decision = ConflictResolver.Resolve(ConflictFixtures.InputsFor(ConflictFixtures.Named(caseName)), _nothingIsDeclared);

        Assert.Equal(ConflictOutcome.Refuse, decision.Outcome);
        Assert.Null(decision.Rule);
    }

    /// <summary>
    /// Every arrangement the resolver can be handed refuses when nothing is
    /// declared, and the value it is left holding is this server's own.
    /// </summary>
    /// <remarks>
    /// This is the whole reachable input surface rather than the rows somebody
    /// wrote down: both values across absence, emptiness, whitespace and two
    /// different texts, what this plugin last wrote across absence and each of
    /// the two texts, and every combination of the three lock answers. A
    /// default that fired for one shape of input and not another would be
    /// reached here and would not be reached by a fixture table.
    /// <para>
    /// The value is compared by reference. A refusal that hands back the peer's
    /// value has written the peer's value in every sense that matters to the
    /// caller, and one that rebuilds this server's value out of the two is the
    /// merged answer the outcome set is closed against.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryArrangementOfTheInputSurfaceRefusesWhenNothingIsDeclared()
    {
        var swept = 0;

        foreach (var inputs in EveryArrangement())
        {
            var decision = ConflictResolver.Resolve(inputs, _nothingIsDeclared);

            Assert.Equal(ConflictOutcome.Refuse, decision.Outcome);
            Assert.Null(decision.Rule);
            Assert.True(
                ReferenceEquals(decision.Value, inputs.LocalValue),
                "A refusal handed back a value that is not the one this server holds.");

            swept++;
        }

        // Named rather than left implicit, so a sweep that quietly stopped
        // building arrangements fails here instead of passing over three of
        // them. Five local values, five peer values, three prior writes and
        // three lock answers taken two ways each.
        Assert.Equal(5 * 5 * 3 * 2 * 2 * 2, swept);
    }

    /// <summary>
    /// The neighbour, and the reason the sweep above is about the empty table
    /// rather than about a resolver that refuses whatever it is given. One
    /// declared rule, an arrangement that fires it, and the peer's value comes
    /// back under that rule's name.
    /// </summary>
    [Fact]
    public void OneDeclaredRuleThatFiresIsAnsweredByThatRule()
    {
        var inputs = new ConflictInputs
        {
            LocalValue = null,
            PeerValue = "From the peer",
            LastWrittenByThisPlugin = null,
            ItemLockedHere = false,
            FieldLockedHere = false,
            FieldLockedOnPeer = false,
        };

        var decision = ConflictResolver.Resolve(inputs, OnlyTheRule("local-value-absent"));

        Assert.Equal(ConflictOutcome.TakePeer, decision.Outcome);
        Assert.Equal("local-value-absent", decision.Rule?.Id);
        Assert.True(ReferenceEquals(decision.Value, inputs.PeerValue), "The peer's value was not the value taken.");
    }

    /// <summary>
    /// The same arrangement with the rule removed and nothing else changed.
    /// Read beside the test above, this is the pair that says the answer came
    /// from the declaration: one rule takes the peer's value, no rules refuses,
    /// and the resolver is the same resolver in both calls.
    /// </summary>
    [Fact]
    public void TheSameArrangementRefusesWithThatRuleTakenAway()
    {
        var inputs = new ConflictInputs
        {
            LocalValue = null,
            PeerValue = "From the peer",
            LastWrittenByThisPlugin = null,
            ItemLockedHere = false,
            FieldLockedHere = false,
            FieldLockedOnPeer = false,
        };

        var decision = ConflictResolver.Resolve(inputs, _nothingIsDeclared);

        Assert.Equal(ConflictOutcome.Refuse, decision.Outcome);
        Assert.Null(decision.Rule);
    }

    /// <summary>
    /// The declared table with everything but one rule taken out of it.
    /// </summary>
    /// <param name="id">The rule to keep.</param>
    /// <returns>A rule set of one rule.</returns>
    private static IReadOnlyList<ConflictRule> OnlyTheRule(string id)
    {
        var kept = ConflictRules.Rules.Where(rule => string.Equals(rule.Id, id, StringComparison.Ordinal)).ToList();

        Assert.Single(kept);
        return kept;
    }

    /// <summary>
    /// Every combination of the six things a conflict is decided from.
    /// </summary>
    /// <returns>The arrangements.</returns>
    private static IEnumerable<ConflictInputs> EveryArrangement()
    {
        foreach (var local in _localValues)
        {
            foreach (var peer in _peerValues)
            {
                foreach (var written in _lastWritten)
                {
                    foreach (var itemLocked in new[] { false, true })
                    {
                        foreach (var fieldLocked in new[] { false, true })
                        {
                            foreach (var peerLocked in new[] { false, true })
                            {
                                yield return new ConflictInputs
                                {
                                    LocalValue = local,
                                    PeerValue = peer,
                                    LastWrittenByThisPlugin = written,
                                    ItemLockedHere = itemLocked,
                                    FieldLockedHere = fieldLocked,
                                    FieldLockedOnPeer = peerLocked,
                                };
                            }
                        }
                    }
                }
            }
        }
    }
}
