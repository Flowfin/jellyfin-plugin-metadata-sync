using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// No conflict is decided on a clock, held from both ends: nothing the resolver
/// is handed can carry a time, and no declared rule turns on one.
/// </summary>
/// <remarks>
/// The rule this file holds up is that two independent servers have no shared
/// time source, so a comparison between their clocks hands every field to
/// whichever machine is ahead, permanently and with nothing reporting it. The
/// argument is in <c>docs/conflicts.md</c>; what is here is the pair of checks
/// that make it a property rather than a paragraph.
/// <para>
/// The invariant lint already refuses the spellings a clock arrives under in
/// the plugin's own source text, and that is a different reach from this one. A
/// token scan reads lines somebody wrote; these two read the input surface and
/// the declared table, which is where a clock would arrive without any of those
/// spellings appearing. Neither is a substitute for the other, and this remark
/// is here so that a reader who found one does not stop looking.
/// </para>
/// <para>
/// The input check refuses by shape rather than by name, which is the direction
/// that fails closed. A guard naming <c>DateTime</c> is walked around by a
/// <c>long</c> of ticks, and a guard naming both is walked around by the next
/// spelling somebody invents. What is asserted instead is that every input is
/// one of the two shapes a conflict input is made of, so anything else is
/// refused whatever it is called and whoever adds it has to argue for it here.
/// </para>
/// </remarks>
public class ConflictClockTests
{
    /// <summary>
    /// A rule that decides on which side changed more recently, which is the
    /// rule this issue exists to refuse and the one every prior attempt in this
    /// space reaches for. The outcome it names is a legal one, so nothing
    /// earlier in the read refuses it on the way past.
    /// </summary>
    private const string NewestWinsRules = """
        { "rules": [ { "id": "newest-wins", "condition": "The peer's value is newer than this server's.", "outcome": "TakePeer", "reason": "The more recent change is usually the one somebody meant." } ] }
        """;

    /// <summary>
    /// The words a rule reaches for when it is deciding on time. They are read
    /// against a rule's condition, which is the sentence that says when it
    /// fires, rather than against its reason, which is free to argue about
    /// clocks and in this table does.
    /// </summary>
    private static readonly string[] _timeWords =
    [
        "newer", "newest", "older", "oldest", "more recent", "most recent",
        "last saved", "last modified", "timestamp", "clock", "DateLastSaved", "DateModified",
    ];

    /// <summary>
    /// The shapes a conflict input is made of. A value on one side or the
    /// other, and an answer about a lock.
    /// </summary>
    private static readonly Type[] _shapesAnInputIsMadeOf = [typeof(string), typeof(bool)];

    /// <summary>
    /// The resolver is handed no clock, under any name and in any shape. This
    /// is the whole input surface, so a rule cannot reach for a time that was
    /// never passed to it.
    /// </summary>
    [Fact]
    public void TheResolverIsHandedNoClockOfAnyKind()
    {
        Assert.Empty(NotAShapeAnInputIsMadeOf(typeof(ConflictInputs)));
    }

    /// <summary>
    /// The bite. A timestamp added to the input surface is refused, in the
    /// spelling somebody would actually add it in.
    /// </summary>
    [Fact]
    public void AnInputCarryingATimestampIsRefused()
    {
        Assert.NotEmpty(NotAShapeAnInputIsMadeOf(typeof(InputsCarryingATimestamp)));
    }

    /// <summary>
    /// The bite again, one spelling further out. A guard that named the time
    /// types would pass this, and it is the same clock with the type changed.
    /// </summary>
    [Fact]
    public void AnInputCarryingAClockSpelledAsANumberIsRefused()
    {
        Assert.NotEmpty(NotAShapeAnInputIsMadeOf(typeof(InputsCarryingTicks)));
    }

    /// <summary>
    /// The neighbour. A guard that refused everything would be as useless as
    /// one that refused nothing, so an input that is a value and an input that
    /// is an answer about a lock both pass, which is what a legitimate addition
    /// to the surface looks like.
    /// </summary>
    [Fact]
    public void AnInputInTheShapesAConflictIsDecidedFromIsAccepted()
    {
        Assert.Empty(NotAShapeAnInputIsMadeOf(typeof(InputsInTheOrdinaryShapes)));
    }

    /// <summary>
    /// No declared rule fires on which side is more recent. The conditions are
    /// read rather than the reasons, because a reason arguing against clocks is
    /// what this table wants and a condition turning on one is what it refuses.
    /// </summary>
    [Fact]
    public void NoDeclaredRuleTurnsOnAClock()
    {
        Assert.Empty(ConflictRules.Rules.SelectMany(rule => TimeWordsIn(rule.Condition)).ToList());
    }

    /// <summary>
    /// The bite for the table. The rule refused here is the one the plan
    /// rejected by name in decision 2, and a rule set carrying it reads
    /// perfectly well otherwise.
    /// </summary>
    [Fact]
    public void ARuleThatFiresOnWhichSideIsNewerIsRefused()
    {
        var newestWins = ConflictRules.Parse(NewestWinsRules);

        Assert.NotEmpty(newestWins.SelectMany(rule => TimeWordsIn(rule.Condition)).ToList());
    }

    /// <summary>
    /// The neighbour for the table. The rule that does the same work causally
    /// is the one the shipped table carries, and it is a sentence about what
    /// this plugin last wrote rather than about when anything happened.
    /// </summary>
    [Fact]
    public void TheRuleThatAnswersTheSameQuestionCausallyIsAccepted()
    {
        var causal = ConflictRules.Find("local-unchanged-since-this-plugin-wrote-it");

        Assert.NotNull(causal);
        Assert.Empty(TimeWordsIn(causal.Condition));
    }

    /// <summary>
    /// The properties on a type that are not one of the shapes a conflict input
    /// is made of, nullable forms counting as the shape they wrap.
    /// </summary>
    private static IReadOnlyList<string> NotAShapeAnInputIsMadeOf(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => !_shapesAnInputIsMadeOf.Contains(
                Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType))
            .Select(property => type.Name + "." + property.Name + " is a " + property.PropertyType.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The time words a sentence uses, read without regard to case because the
    /// sentence is prose and not a token.
    /// </summary>
    private static IReadOnlyList<string> TimeWordsIn(string condition)
    {
        return _timeWords
            .Where(word => condition.Contains(word, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// The input surface with the obvious timestamp added to it.
    /// </summary>
    private sealed class InputsCarryingATimestamp
    {
        public string? LocalValue { get; init; }

        public DateTime PeerLastSaved { get; init; }
    }

    /// <summary>
    /// The input surface with the same clock arriving as a number, which is how
    /// it arrives once somebody has been told not to pass a date.
    /// </summary>
    private sealed class InputsCarryingTicks
    {
        public string? LocalValue { get; init; }

        public long PeerLastSavedTicks { get; init; }
    }

    /// <summary>
    /// An input surface somebody widened legitimately, which this may not
    /// refuse.
    /// </summary>
    private sealed class InputsInTheOrdinaryShapes
    {
        public string? LocalValue { get; init; }

        public string? PeerValue { get; init; }

        public bool ItemLockedOnThePeer { get; init; }
    }
}
