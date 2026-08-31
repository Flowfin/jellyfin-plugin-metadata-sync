using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The lines an operator opens instead of the rows, which is #48's second
/// condition: entries grouped by rule and by reason, with the grouping derived
/// rather than stored twice.
/// </summary>
/// <remarks>
/// The condition's testable half is where the grouping comes from, and that is
/// what these cases are about. Two legs hold the derivation itself - every
/// decision reaches exactly one group, and the groups add nothing that was not
/// in the rows - and two more refuse the shape a second copy would arrive in: a
/// number kept beside the members, and a group held on any type this plugin has.
/// <para>
/// The other half of the condition is a surface, and there is none. Nothing here
/// renders anything, and a green run says a set of decisions falls into the
/// lines this plugin claims it does, not that anybody can see them.
/// </para>
/// </remarks>
public class ConflictGroupingTests
{
    private static readonly DateTimeOffset _at = new(2026, 8, 31, 21, 0, 0, TimeSpan.Zero);

    private static readonly string[] _threeFields = { "Overview", "Tagline", "Genres" };
    private static readonly string?[] _twoRules = { "peer-field-locked", "item-locked-here" };
    private static readonly string?[] _aRuleThenNone = { "item-locked-here", null };
    private static readonly string[] _oneField = { "Overview" };
    private static readonly string[] _twoFields = { "Tagline", "Genres" };

    /// <summary>
    /// One missing rule across a library is one line rather than one line per
    /// item, which is the whole reason this reading exists.
    /// </summary>
    [Fact]
    public void DecisionsOneRuleTookToOneEndAreOneLine()
    {
        var groups = ConflictGrouping.Of(new[]
        {
            Decided("Overview", rule: null, ConflictOutcome.Refuse),
            Decided("Tagline", rule: null, ConflictOutcome.Refuse),
            Decided("Genres", rule: null, ConflictOutcome.Refuse),
        });

        var group = Assert.Single(groups);
        Assert.Null(group.Rule);
        Assert.Equal(ConflictOutcome.Refuse, group.Outcome);
        Assert.Equal(_threeFields, group.Entries.Select(e => e.Field));
    }

    /// <summary>
    /// The rule and the outcome both separate two lines, because either one on
    /// its own answers a question an operator is not asking.
    /// </summary>
    [Fact]
    public void TheRuleAndTheOutcomeBothSeparateTwoLines()
    {
        var byOutcome = ConflictGrouping.Of(new[]
        {
            Decided("Overview", "peer-field-locked", ConflictOutcome.Refuse),
            Decided("Tagline", "peer-field-locked", ConflictOutcome.KeepLocal),
        });

        Assert.Equal(2, byOutcome.Count);

        var byRule = ConflictGrouping.Of(new[]
        {
            Decided("Overview", "peer-field-locked", ConflictOutcome.Refuse),
            Decided("Tagline", "item-locked-here", ConflictOutcome.Refuse),
        });

        Assert.Equal(2, byRule.Count);
        Assert.Equal(_twoRules, byRule.Select(g => g.Rule));
    }

    /// <summary>
    /// A decision no rule answered is its own line and is not filed under a rule
    /// somebody named with nothing.
    /// </summary>
    /// <remarks>
    /// This is the one-character mistake. A key spelled as the rule or the empty
    /// string collapses the two, and the collapsed line says this plan has an
    /// answer for a disagreement it has no answer for.
    /// </remarks>
    [Fact]
    public void NoRuleAtAllIsNotFiledUnderARuleNamedWithNothing()
    {
        var groups = ConflictGrouping.Of(new[]
        {
            Decided("Overview", rule: null, ConflictOutcome.Refuse),
            Decided("Tagline", string.Empty, ConflictOutcome.Refuse),
        });

        Assert.Equal(2, groups.Count);
        Assert.Null(groups[0].Rule);
        Assert.Equal(string.Empty, groups[1].Rule);
    }

    /// <summary>
    /// Every decision reaches exactly one group and the groups carry nothing the
    /// rows did not, which is the runnable form of derived rather than stored
    /// twice.
    /// </summary>
    /// <remarks>
    /// Taken apart and put back together rather than counted. A count comparison
    /// passes on a grouping that lost one decision and duplicated another, and
    /// duplication is what a reading filing an entry under two keys produces.
    /// </remarks>
    [Fact]
    public void EveryDecisionReachesExactlyOneGroupAndTheGroupsAddNothing()
    {
        var entries = new[]
        {
            Decided("Overview", "peer-field-locked", ConflictOutcome.Refuse),
            Decided("Tagline", null, ConflictOutcome.Refuse),
            Decided("Genres", "peer-field-locked", ConflictOutcome.TakePeer),
            Decided("Studios", "peer-field-locked", ConflictOutcome.Refuse),
            Decided("SortName", null, ConflictOutcome.KeepLocal),
        };

        var groups = ConflictGrouping.Of(entries);
        var rebuilt = groups.SelectMany(g => g.Entries).ToList();

        Assert.Equal(entries.Length, rebuilt.Count);

        foreach (var entry in entries)
        {
            Assert.Single(rebuilt, held => ReferenceEquals(held, entry));
        }

        // Every member is under the line it says it belongs to, so a group
        // cannot carry a rule and an outcome its own rows disagree with.
        foreach (var group in groups)
        {
            Assert.All(group.Entries, entry =>
            {
                Assert.Equal(group.Rule, entry.Rule);
                Assert.Equal(group.Outcome, entry.Outcome);
            });
        }
    }

    /// <summary>
    /// A group appears where its first decision appeared, and its members keep
    /// the order the account holds them in.
    /// </summary>
    /// <remarks>
    /// The order is what says when a line started happening, which is the
    /// question an operator brings to a log. Sorting by size would put the rule
    /// that is working at the top, so the lines below are deliberately of
    /// different sizes with the smaller one first.
    /// </remarks>
    [Fact]
    public void TheGroupsFollowTheOrderTheAccountHoldsThemIn()
    {
        var groups = ConflictGrouping.Of(new[]
        {
            Decided("Overview", "item-locked-here", ConflictOutcome.KeepLocal),
            Decided("Tagline", null, ConflictOutcome.Refuse),
            Decided("Genres", null, ConflictOutcome.Refuse),
        });

        // The smaller line came first, so an order taken from the sizes rather
        // than from the account puts these the other way round.
        Assert.Equal(_aRuleThenNone, groups.Select(g => g.Rule));
        Assert.Equal(_oneField, groups[0].Entries.Select(e => e.Field));
        Assert.Equal(_twoFields, groups[1].Entries.Select(e => e.Field));
    }

    /// <summary>
    /// A pass with nothing to tell has no lines rather than one line holding
    /// nothing.
    /// </summary>
    [Fact]
    public void AnAccountWithNothingToTellHasNoLines()
    {
        Assert.Empty(ConflictGrouping.Of(Array.Empty<ConflictEntry>()));
    }

    /// <summary>
    /// There is nothing to read, and the refusal is at the call rather than
    /// whenever somebody gets round to the answer.
    /// </summary>
    [Fact]
    public void NoDecisionsAtAllIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => ConflictGrouping.Of(null!));
    }

    /// <summary>
    /// A decision that is not there is refused rather than filed under a line,
    /// so a hole in the account is a refusal naming what was asked for and not a
    /// reference that failed somewhere inside the reading.
    /// </summary>
    [Fact]
    public void ADecisionThatIsNotThereIsRefused()
    {
        Assert.Throws<ArgumentNullException>(
            () => ConflictGrouping.Of(new ConflictEntry[] { null! }));
    }

    /// <summary>
    /// A group carries no number beside its members, so there is no second total
    /// for the list under it to disagree with.
    /// </summary>
    /// <remarks>
    /// This is the refusal the condition asks for, written against the shape
    /// rather than as a comparison that would pass on the day the two happen to
    /// agree. A count added to the group reds here at the moment it is written,
    /// which is before it can drift.
    /// </remarks>
    [Fact]
    public void AGroupCarriesNoNumberBesideItsMembers()
    {
        var restated = typeof(ConflictGroup)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => IsANumber(p.PropertyType))
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(restated);
    }

    /// <summary>
    /// Nothing this plugin holds carries a group, so the grouping is a reading
    /// taken when somebody asks and never a second account written down beside
    /// the rows.
    /// </summary>
    [Fact]
    public void NothingThisPluginHoldsCarriesAGroup()
    {
        Assert.Empty(MembersMadeOfAGroup(typeof(Plugin).Assembly));
    }

    /// <summary>
    /// The sweep above finds one where there is one, so an empty answer is a
    /// tree holding no group rather than a sweep that read nothing.
    /// </summary>
    [Fact]
    public void TheSweepFindsAGroupHeldOnAType()
    {
        var found = MembersMadeOfAGroup(typeof(ConflictGroupingTests).Assembly);

        Assert.Contains("HeldGroups.Lines", found, StringComparer.Ordinal);
        Assert.Contains("HeldGroups.One", found, StringComparer.Ordinal);
    }

    /// <summary>
    /// Every member of every type in an assembly that is made of a group,
    /// directly or as what a collection holds.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The members, named type by member.</returns>
    private static IReadOnlyList<string> MembersMadeOfAGroup(Assembly assembly)
    {
        const BindingFlags Everything = BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        var found = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            foreach (var property in type.GetProperties(Everything))
            {
                if (IsMadeOfAGroup(property.PropertyType))
                {
                    found.Add(type.Name + "." + property.Name);
                }
            }

            foreach (var field in type.GetFields(Everything))
            {
                if (IsMadeOfAGroup(field.FieldType))
                {
                    found.Add(type.Name + "." + field.Name);
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Whether a member's type is a group, or a collection of them.
    /// </summary>
    /// <param name="type">The member's type.</param>
    /// <returns>True where a group is held.</returns>
    private static bool IsMadeOfAGroup(Type type)
    {
        if (type == typeof(ConflictGroup))
        {
            return true;
        }

        if (type.IsArray)
        {
            return IsMadeOfAGroup(type.GetElementType()!);
        }

        return type.IsGenericType
            && Array.Exists(type.GetGenericArguments(), IsMadeOfAGroup);
    }

    /// <summary>
    /// Whether a type is a number, which is what a restated count is spelled as.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>True where the type counts something.</returns>
    private static bool IsANumber(Type type)
    {
        var bare = Nullable.GetUnderlyingType(type) ?? type;

        return bare == typeof(int)
            || bare == typeof(long)
            || bare == typeof(short)
            || bare == typeof(uint)
            || bare == typeof(ulong)
            || bare == typeof(ushort)
            || bare == typeof(byte)
            || bare == typeof(sbyte)
            || bare == typeof(decimal)
            || bare == typeof(double)
            || bare == typeof(float);
    }

    /// <summary>
    /// One decision, as the account holds it.
    /// </summary>
    /// <param name="field">The field decided.</param>
    /// <param name="rule">The rule that decided it, or null where none did.</param>
    /// <param name="outcome">What happened to the field.</param>
    /// <returns>The entry.</returns>
    private static ConflictEntry Decided(string field, string? rule, ConflictOutcome outcome) => new()
    {
        Item = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
        Field = field,
        LocalValue = ShownValue.Of("Ours"),
        PeerValue = ShownValue.Of("Theirs"),
        Rule = rule,
        Outcome = outcome,
        Direction = SyncDirection.TwoWay,
        At = _at,
    };

    /// <summary>
    /// A type holding groups, which nothing in the plugin does. It exists so the
    /// sweep above is exercised on something rather than passing over a tree
    /// that has nothing in it.
    /// </summary>
    private sealed class HeldGroups
    {
        /// <summary>
        /// Gets groups held as a list, which is how a second account would
        /// arrive.
        /// </summary>
        public IReadOnlyList<ConflictGroup> Lines { get; } = Array.Empty<ConflictGroup>();

        /// <summary>
        /// Gets one group held on its own, which a collection reading alone
        /// would miss.
        /// </summary>
        public ConflictGroup? One { get; }
    }
}
