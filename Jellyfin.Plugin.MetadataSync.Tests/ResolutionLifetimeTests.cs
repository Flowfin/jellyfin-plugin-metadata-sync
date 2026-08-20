using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Matching;
using Jellyfin.Plugin.MetadataSync.References;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Keeps a resolution inside the call that derived it.
/// </summary>
/// <remarks>
/// A resolution says which item on the other side a work is. It is true of two
/// libraries as they stood when it was computed, and both of them move: an
/// operator runs a metadata scan, merges two cuts of a film, or deletes an item.
/// So a resolution read again on a later pass is an answer about libraries that
/// no longer exist, and the write it feeds lands on whatever now sits where the
/// old answer pointed. Nothing about the result says an old answer was used.
///
/// The failure needs somewhere to live before it can happen. A value returned
/// from a call and never stored dies with the frame that received it, so the
/// only way one reaches a second pass is a slot that outlives the first: a
/// field, or a property, on something a later pass can reach. This refuses the
/// slot rather than the reuse, which is what can be decided from this tree
/// today. Whether a pass re-derives before it writes is a question about a pass,
/// and there is no pass here yet. #33 carries both halves and this is the half
/// that does not have to wait for one.
///
/// A plan row is inside the subject on purpose rather than by accident. A plan
/// is the artefact #38 makes resumable, so a resolution stored on one is
/// precisely a resolution that outlives the pass that derived it, and it would
/// be read back by whichever pass resumes. What a plan carries is the value that
/// was decided, and the row already has columns for it.
///
/// What this cannot catch, stated rather than left to be assumed. It reads
/// declared member types, so a resolution kept in a slot typed <c>object</c>, in
/// a dictionary keyed to it, or behind a cache in another process spells none of
/// this. It reads this plugin's assembly, so a resolution the server itself
/// holds is outside it. It reads members and never method bodies, so a
/// resolution carried through a closure that the compiler lifts is out of reach.
/// And it says nothing about whether the one place that derives a resolution
/// derives it at the right moment, which is the behavioural half of the same
/// condition.
/// </remarks>
public class ResolutionLifetimeTests
{
    /// <summary>
    /// A resolver is a type this plugin declares whose name ends this way, and a
    /// resolution is what one of its entry points answers with. Both sets are
    /// read out of the assembly rather than listed here, so a fourth resolver is
    /// covered on the day it lands instead of on the day somebody remembers.
    /// </summary>
    private const string ResolverSuffix = "Resolver";

    /// <summary>
    /// The prefix an entry point carries. <c>RuleFor</c>, <c>StepFor</c> and
    /// <c>Statement</c> sit on the same types and answer with a rule, a step or
    /// a sentence, none of which is an answer about a particular pair of
    /// values.
    /// </summary>
    private const string EntryPointPrefix = "Resolve";

    /// <summary>
    /// The rule. Nothing in this plugin holds a resolution.
    /// </summary>
    [Fact]
    public void NothingInThePluginHoldsAResolution()
    {
        var assembly = typeof(Plugin).Assembly;

        Assert.Empty(HoldersOf(ResolutionsAnsweredBy(assembly), assembly));
    }

    /// <summary>
    /// The walk reads types. If it ever reads none, or finds no resolver among
    /// them, it refuses nothing and passes for the wrong reason, so what it
    /// found is asserted before anything is concluded from a clean run.
    /// </summary>
    [Fact]
    public void TheWalkFindsTheResolversThatAreInTheTree()
    {
        var resolvers = ResolversIn(typeof(Plugin).Assembly)
            .Select(type => type.Name)
            .ToList();

        Assert.NotEmpty(resolvers);
        Assert.Contains(nameof(CandidateResolver), resolvers, StringComparer.Ordinal);
        Assert.Contains(nameof(OrdinalResolver), resolvers, StringComparer.Ordinal);
        Assert.Contains(nameof(ReferenceResolver), resolvers, StringComparer.Ordinal);
        Assert.Contains(nameof(ConflictResolver), resolvers, StringComparer.Ordinal);
    }

    /// <summary>
    /// The second half of the same guard, against a walk that reads the right
    /// types and derives nothing from them. A resolver whose entry point was
    /// renamed contributes no resolution and would quietly leave its own answers
    /// unguarded, so every resolver found has to answer with at least one.
    /// </summary>
    [Fact]
    public void EveryResolverFoundAnswersWithAResolution()
    {
        var assembly = typeof(Plugin).Assembly;
        var silent = ResolversIn(assembly)
            .Where(resolver => ResolutionsAnsweredBy(resolver).Count == 0)
            .Select(resolver => resolver.Name)
            .ToList();

        Assert.NotEmpty(ResolutionsAnsweredBy(assembly));
        Assert.Empty(silent);
    }

    /// <summary>
    /// The bite. Run against types that do what the rule refuses, the walk finds
    /// each of them. They live in the test assembly, so the proof does not
    /// depend on the plugin carrying a defect for the guard to be readable.
    /// </summary>
    [Fact]
    public void TheWalkFindsAResolutionHeldInEachOfTheThreeShapes()
    {
        var held = HoldersOf(
            ResolutionsAnsweredBy(typeof(Plugin).Assembly),
            typeof(ResolutionLifetimeTests).Assembly);

        Assert.Contains(nameof(HoldsOneOutright) + ".Kept", held, StringComparer.Ordinal);
        Assert.Contains(nameof(HoldsOneInACollection) + ".Kept", held, StringComparer.Ordinal);
        Assert.Contains(nameof(HoldsOneBehindAProperty) + ".Kept", held, StringComparer.Ordinal);
    }

    /// <summary>
    /// The neighbour. The resolvers hold their comparison tables in exactly the
    /// shape the collection case above is refused for, and a rule that took the
    /// return type of every method on a resolver instead of its entry points
    /// would refuse those tables and be switched off within a week.
    /// </summary>
    [Fact]
    public void TheWalkLeavesARuleTableHeldTheSameWayAlone()
    {
        var held = HoldersOf(
            ResolutionsAnsweredBy(typeof(Plugin).Assembly),
            typeof(ResolutionLifetimeTests).Assembly);

        Assert.DoesNotContain(nameof(HoldsARuleTable) + ".Kept", held, StringComparer.Ordinal);
    }

    /// <summary>
    /// Every member of every type in an assembly that holds one of the given
    /// types, named as its declaring type and the member. Fields and properties
    /// both, static and instance both: a singleton service holds one in an
    /// instance field and a helper holds one in a static.
    /// </summary>
    /// <param name="resolutions">The types a slot may not hold.</param>
    /// <param name="assembly">The assembly whose declared members are read.</param>
    /// <returns>The members that hold one, ordered.</returns>
    private static IReadOnlyList<string> HoldersOf(IReadOnlyCollection<Type> resolutions, Assembly assembly)
    {
        const BindingFlags Everything =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance |
            BindingFlags.DeclaredOnly;

        var held = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            foreach (var field in type.GetFields(Everything).Where(field => Carries(field.FieldType, resolutions)))
            {
                held.Add(type.Name + "." + field.Name);
            }

            foreach (var property in type.GetProperties(Everything).Where(property => Carries(property.PropertyType, resolutions)))
            {
                held.Add(type.Name + "." + property.Name);
            }
        }

        return held.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Whether a declared member type reaches one of the given types. A
    /// resolution put behind an array, a nullable or a collection is the same
    /// resolution, so the arguments are followed rather than the outermost name
    /// being compared.
    /// </summary>
    /// <param name="declared">The type the member declares.</param>
    /// <param name="resolutions">The types a slot may not hold.</param>
    /// <returns>Whether the member reaches one.</returns>
    private static bool Carries(Type declared, IReadOnlyCollection<Type> resolutions)
    {
        if (resolutions.Contains(declared))
        {
            return true;
        }

        var element = declared.GetElementType();
        if (element is not null && Carries(element, resolutions))
        {
            return true;
        }

        return declared.IsGenericType
            && declared.GetGenericArguments().Any(argument => Carries(argument, resolutions));
    }

    /// <summary>
    /// The resolvers an assembly declares.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The resolver types, ordered.</returns>
    private static IReadOnlyList<Type> ResolversIn(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(type => type.Name.EndsWith(ResolverSuffix, StringComparison.Ordinal))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// What every resolver in an assembly answers with.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The resolution types.</returns>
    private static IReadOnlyCollection<Type> ResolutionsAnsweredBy(Assembly assembly)
    {
        return ResolversIn(assembly).SelectMany(ResolutionsAnsweredBy).ToHashSet();
    }

    /// <summary>
    /// What one resolver answers with: the types this plugin declares that its
    /// public entry points return. The return type is unwrapped rather than
    /// compared whole, so a resolver that later answers asynchronously
    /// contributes the answer inside the task instead of nothing.
    ///
    /// An enum is left out. A verdict member is a name for what was decided and
    /// is held all over a plan legitimately; what carries the decision itself is
    /// the type around it.
    /// </summary>
    /// <param name="resolver">The resolver to read.</param>
    /// <returns>The resolution types it answers with.</returns>
    private static IReadOnlyCollection<Type> ResolutionsAnsweredBy(Type resolver)
    {
        return resolver
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name.StartsWith(EntryPointPrefix, StringComparison.Ordinal))
            .SelectMany(method => DeclaredWithin(method.ReturnType, resolver.Assembly))
            .ToHashSet();
    }

    /// <summary>
    /// The types an assembly declares that a return type reaches, itself
    /// included, following arrays and generic arguments.
    /// </summary>
    /// <param name="returned">The type an entry point returns.</param>
    /// <param name="assembly">The assembly whose own types are wanted.</param>
    /// <returns>The types it reaches.</returns>
    private static IEnumerable<Type> DeclaredWithin(Type returned, Assembly assembly)
    {
        if (returned.Assembly == assembly && !returned.IsEnum)
        {
            yield return returned;
        }

        var element = returned.GetElementType();
        if (element is not null)
        {
            foreach (var inner in DeclaredWithin(element, assembly))
            {
                yield return inner;
            }
        }

        if (!returned.IsGenericType)
        {
            yield break;
        }

        foreach (var inner in returned.GetGenericArguments().SelectMany(argument => DeclaredWithin(argument, assembly)))
        {
            yield return inner;
        }
    }

    /// <summary>
    /// A resolution kept in a field, which is the shape a cache takes.
    /// </summary>
    private static class HoldsOneOutright
    {
        /// <summary>Gets the slot the rule refuses.</summary>
        public static CandidateResolution? Kept { get; }
    }

    /// <summary>
    /// A resolution kept inside a collection, which is what a register of them
    /// looks like and what a rule comparing the outermost type would miss.
    /// </summary>
    private static class HoldsOneInACollection
    {
        /// <summary>Gets the slot the rule refuses.</summary>
        public static IReadOnlyList<OrdinalResolution> Kept { get; } = Array.Empty<OrdinalResolution>();
    }

    /// <summary>
    /// A resolution behind an array, which is the same slot written the way a
    /// second pass would keep last pass's answers.
    /// </summary>
    private static class HoldsOneBehindAProperty
    {
        /// <summary>Gets the slot the rule refuses.</summary>
        public static ReferenceResolution[] Kept { get; } = Array.Empty<ReferenceResolution>();
    }

    /// <summary>
    /// The neighbour: a comparison table held in a collection, in the shape the
    /// resolvers already hold theirs. It is loaded from an embedded resource and
    /// says nothing about any pair of libraries, so keeping it is what these
    /// types are for.
    /// </summary>
    private static class HoldsARuleTable
    {
        /// <summary>Gets the slot the rule leaves alone.</summary>
        public static IReadOnlyList<ComparisonRule> Kept { get; } = Array.Empty<ComparisonRule>();
    }
}
