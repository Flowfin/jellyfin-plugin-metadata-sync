using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jellyfin.Plugin.MetadataSync.Configuration;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Jellyfin.Plugin.MetadataSync.Fields;
using Jellyfin.Plugin.MetadataSync.Matching;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Every place the plugin refuses is named here, against the test that trips it
/// and the neighbour that differs by one thing and is not refused. A refusal
/// nobody trips has never been seen to happen, and it is indistinguishable from
/// a line that cannot be reached at all.
/// </summary>
/// <remarks>
/// The behavioural tests this register names live beside the type they are
/// about, and a second copy of an assertion is a copy that drifts. What is
/// added here is the register, which is the part that keeps working after it is
/// written: a guard added to the plugin later with no entry fails the suite
/// rather than waiting for somebody to notice it in review, and a guard deleted
/// while its entry stays fails it the same way. Both directions, because a
/// register that only grows stops describing the tree the moment a guard moves.
/// <para>
/// Each entry also carries the arrangement that reaches its site, and the
/// register runs it. That is not a copy of the named test's assertion: the
/// named test asserts what the caller sees, and the register asserts where the
/// refusal came from, which is the thing naming a test cannot establish.
/// </para>
/// </remarks>
public class RefusalTests
{
    /// <summary>
    /// One entry per refusal site the plugin carries. The key is the file the
    /// site is in and the line of code that refuses, so an edit to the guard
    /// itself shows up here rather than passing under a line number that
    /// happens to still match.
    /// </summary>
    private static readonly Dictionary<string, (string TestClass, string Trip, string Neighbour, string DiffersBy, Action Reaches)> Register =
        new(StringComparer.Ordinal)
        {
            ["Configuration/PluginConfigurationProvider.cs -> ArgumentNullException.ThrowIfNull(read);"] =
                (nameof(ServiceRegistrationTests),
                 nameof(ServiceRegistrationTests.ConfigurationProviderRefusesAMissingReader),
                 nameof(ServiceRegistrationTests.ConfigurationProviderReturnsTheConfigurationItWasGiven),
                 "the delegate is there",
                 () => new PluginConfigurationProvider(null!)),

            ["Fields/FieldMover.cs -> ArgumentNullException.ThrowIfNull(from);"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.MovingFromAnItemThatIsNotThereIsRefused),
                 nameof(FieldRegisterTests.ADeclaredFieldIsWrittenOntoTheItem),
                 "the item the value is taken from is there",
                 () => FieldMover.Move("Overview", null!, new Movie())),

            ["Fields/FieldMover.cs -> ArgumentNullException.ThrowIfNull(to);"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.MovingOntoAnItemThatIsNotThereIsRefused),
                 nameof(FieldRegisterTests.ADeclaredFieldIsWrittenOntoTheItem),
                 "the item the value is written to is there",
                 () => FieldMover.Move("Overview", new Movie(), null!)),

            ["Fields/FieldMover.cs -> throw new FieldLockedException(TheWholeItemIsLocked(field));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.NoFieldIsWrittenOntoAnItemTheOperatorLocked),
                 nameof(FieldRegisterTests.ADeclaredFieldIsWrittenOntoTheItem),
                 "the operator has not locked the receiving item",
                 () => FieldMover.Move("Tagline", new Movie(), new Movie { IsLocked = true })),

            ["Fields/FieldMover.cs -> throw new FieldLockedException(TheFieldIsLocked(field, governing));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.AFieldTheOperatorLockedIsNotWritten),
                 nameof(FieldRegisterTests.AFieldLockedOnAnotherRowDoesNotRefuseThisOne),
                 "the lock the operator set is not the one governing this field",
                 () => FieldMover.Move("Overview", new Movie(), new Movie { LockedFields = new[] { MetadataField.Overview } })),

            ["Fields/FieldRegister.cs -> throw new InvalidOperationException(NoSuchLock(row));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.ARowNamingALockTheServerDoesNotHaveIsRefused),
                 nameof(FieldRegisterTests.TheRegisterThatShipsInTheAssemblyLoads),
                 "the lock the row names is one the server has",
                 () => FieldRegister.Parse(FieldRegisterTests.LockTheServerDoesNotHaveRegister)),

            ["Fields/FieldRegister.cs -> throw new FieldNotDeclaredException(NoRowAtAll(field));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.AFieldWithNoRowIsRefusedWhenSomethingAsksToMoveIt),
                 nameof(FieldRegisterTests.ADeclaredFieldIsWrittenOntoTheItem),
                 "the field has a row",
                 () => FieldRegister.RequireMovable("PlaybackPositionTicks")),

            ["Fields/FieldRegister.cs -> throw new FieldNotDeclaredException(ARowThatRefuses(row));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.AFieldWhoseRowRefusesToMoveIsRefusedWithItsReason),
                 nameof(FieldRegisterTests.ADeclaredFieldIsWrittenOntoTheItem),
                 "the row the field has says it moves",
                 () => FieldRegister.RequireMovable("RunTimeTicks")),

            ["Fields/FieldRegister.cs -> using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException(NoRegisterEmbedded(resourceName));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.ARegisterThatIsNotEmbeddedIsRefused),
                 nameof(FieldRegisterTests.TheRegisterThatShipsInTheAssemblyLoads),
                 "the assembly carries a register under that name",
                 () => FieldRegister.Load("Jellyfin.Plugin.MetadataSync.Fields.no-such-register.json")),

            ["Fields/FieldRegister.cs -> var read = JsonSerializer.Deserialize<RegisterFile>(text, _json) ?? throw new InvalidOperationException(NothingToRead());"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.RegisterTextThatDescribesNoRegisterIsRefused),
                 nameof(FieldRegisterTests.TheRegisterThatShipsInTheAssemblyLoads),
                 "the text describes a register",
                 () => FieldRegister.Parse("null")),

            ["Fields/FieldRegister.cs -> throw new InvalidOperationException(NoSuchKindGroup(row));"] =
                (nameof(FieldRegisterTests),
                 nameof(FieldRegisterTests.ARowNamingAKindGroupNothingDeclaresIsRefused),
                 nameof(FieldRegisterTests.TheRegisterThatShipsInTheAssemblyLoads),
                 "the group the row names is one the register declares",
                 () => FieldRegister.Parse(FieldRegisterTests.UndeclaredKindGroupRegister)),

            ["Matching/ProviderIdentifiers.cs -> ArgumentNullException.ThrowIfNull(local);"] =
                (nameof(ProviderIdentifierTests),
                 nameof(ProviderIdentifierTests.ComparingWithNoLocalDictionaryIsRefused),
                 nameof(ProviderIdentifierTests.TwoDictionariesThatAreThereAreCompared),
                 "this server's identifiers are there",
                 () => ProviderIdentifiers.Compare(null!, new Dictionary<string, string>(StringComparer.Ordinal))),

            ["Matching/ProviderIdentifiers.cs -> ArgumentNullException.ThrowIfNull(peer);"] =
                (nameof(ProviderIdentifierTests),
                 nameof(ProviderIdentifierTests.ComparingWithNoPeerDictionaryIsRefused),
                 nameof(ProviderIdentifierTests.TwoDictionariesThatAreThereAreCompared),
                 "the peer's identifiers are there",
                 () => ProviderIdentifiers.Compare(new Dictionary<string, string>(StringComparer.Ordinal), null!)),

            ["Matching/ProviderIdentifiers.cs -> using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException(NoTableEmbedded(resourceName));"] =
                (nameof(ProviderIdentifierTests),
                 nameof(ProviderIdentifierTests.ATableThatIsNotEmbeddedIsRefused),
                 nameof(ProviderIdentifierTests.TheTableThatShipsInTheAssemblyLoads),
                 "the assembly carries a table under that name",
                 () => ProviderIdentifiers.Load("Jellyfin.Plugin.MetadataSync.Matching.no-such-table.json")),

            ["Matching/ProviderIdentifiers.cs -> var read = JsonSerializer.Deserialize<TableFile>(text, _json) ?? throw new InvalidOperationException(NothingToRead());"] =
                (nameof(ProviderIdentifierTests),
                 nameof(ProviderIdentifierTests.TableTextThatDescribesNoTableIsRefused),
                 nameof(ProviderIdentifierTests.TheTableThatShipsInTheAssemblyLoads),
                 "the text describes a table",
                 () => ProviderIdentifiers.Parse("null")),

            ["Conflicts/ConflictRules.cs -> using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException(NoRulesEmbedded(resourceName));"] =
                (nameof(ConflictRuleTests),
                 nameof(ConflictRuleTests.ARuleTableThatIsNotEmbeddedIsRefused),
                 nameof(ConflictRuleTests.TheRuleTableThatShipsInTheAssemblyLoads),
                 "the assembly carries a rule table under that name",
                 () => ConflictRules.Load("Jellyfin.Plugin.MetadataSync.Conflicts.no-such-rules.json")),

            ["Conflicts/ConflictRules.cs -> var read = JsonSerializer.Deserialize<RuleFile>(text, _json) ?? throw new InvalidOperationException(NothingToRead());"] =
                (nameof(ConflictRuleTests),
                 nameof(ConflictRuleTests.RuleTextThatDescribesNoRuleSetIsRefused),
                 nameof(ConflictRuleTests.TheRuleTableThatShipsInTheAssemblyLoads),
                 "the text describes a rule set",
                 () => ConflictRules.Parse("null")),

            ["Conflicts/ConflictRules.cs -> throw new InvalidOperationException(NoSuchOutcome(row));"] =
                (nameof(ConflictRuleTests),
                 nameof(ConflictRuleTests.ARuleProducingAnOutcomeNothingDeclaresIsRefused),
                 nameof(ConflictRuleTests.TheRuleTableThatShipsInTheAssemblyLoads),
                 "the outcome the rule produces is one the closed set carries",
                 () => ConflictRules.Parse(ConflictRuleTests.UndeclaredOutcomeRules)),

            ["Conflicts/ConflictRules.cs -> throw new InvalidOperationException(NotArgued(row));"] =
                (nameof(ConflictRuleTests),
                 nameof(ConflictRuleTests.ARuleWithNoReasonIsRefused),
                 nameof(ConflictRuleTests.TheRuleTableThatShipsInTheAssemblyLoads),
                 "the rule says why it is right as well as when it fires",
                 () => ConflictRules.Parse(ConflictRuleTests.UnarguedRules)),

            ["Conflicts/ConflictRules.cs -> throw new InvalidOperationException(TwoRulesUnderOneName(row));"] =
                (nameof(ConflictRuleTests),
                 nameof(ConflictRuleTests.TwoRulesUnderOneNameAreRefused),
                 nameof(ConflictRuleTests.TheRuleTableThatShipsInTheAssemblyLoads),
                 "each rule carries its own name",
                 () => ConflictRules.Parse(ConflictRuleTests.DuplicateNameRules)),
        };

    /// <summary>
    /// Every refusal site the scan finds in the plugin is named in the
    /// register. This is the leg that catches a guard added without a proof.
    /// </summary>
    [Fact]
    public void EveryRefusalSiteInThePluginIsInTheRegister()
    {
        var unregistered = RefusalSites()
            .Where(site => !Register.ContainsKey(site))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(unregistered);
    }

    /// <summary>
    /// Every entry in the register still has a site behind it. This is the leg
    /// that catches a guard deleted while its entry stayed, and it is what
    /// makes deleting a guard turn the suite red for the register as well as
    /// for the test that tripped it.
    /// </summary>
    [Fact]
    public void EveryRegisterEntryStillHasASiteBehindIt()
    {
        var sites = RefusalSites().ToHashSet(StringComparer.Ordinal);

        var dangling = Register.Keys
            .Where(site => !sites.Contains(site))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(dangling);
    }

    /// <summary>
    /// The trip and the neighbour each entry names are tests the suite runs,
    /// and they are two different tests.
    /// </summary>
    /// <remarks>
    /// What this does not do is prove that the named trip is the thing that
    /// reaches the site, or that the neighbour differs from it by the one thing
    /// the entry says. Both pairings are claims made when the entry is written.
    /// The site is reached by the entry's own arrangement, which
    /// <see cref="EveryRegisteredSiteIsObservedToRefuseAtItsOwnLine"/> runs, so
    /// what is left unproved here is the naming and not the reach. The bound is
    /// stated rather than left for a reader to discover.
    /// </remarks>
    [Fact]
    public void EveryRegisterEntryNamesTestsTheSuiteRuns()
    {
        foreach (var entry in Register.Values)
        {
            Assert.NotEqual(entry.Trip, entry.Neighbour);
            Assert.False(string.IsNullOrWhiteSpace(entry.DiffersBy));

            AssertIsAFact(entry.TestClass, entry.Trip);
            AssertIsAFact(entry.TestClass, entry.Neighbour);
        }
    }

    /// <summary>
    /// Every registered site is seen refusing, at the line the register names
    /// it by. This is the leg that separates a register from a proof: the three
    /// legs above are satisfied by an entry naming a line nothing ever reaches,
    /// and this one is not.
    /// </summary>
    /// <remarks>
    /// Reaching the line is not the property. The guard here is spelled
    /// <c>ArgumentNullException.ThrowIfNull</c>, and that line executes on the
    /// neighbour as much as on the trip, so a check that asked only which lines
    /// ran would pass on an arrangement that was never refused at all. What is
    /// asserted instead is where the refusal came from: the exception carries
    /// the frames it was thrown through, and the first of them inside the
    /// plugin is read back out of the source to give the same site string the
    /// scan produces.
    /// </remarks>
    [Fact]
    public void EveryRegisteredSiteIsObservedToRefuseAtItsOwnLine()
    {
        foreach (var (site, entry) in Register)
        {
            var refusal = Record.Exception(entry.Reaches);

            Assert.NotNull(refusal);
            Assert.Equal(site, SiteThatRefused(refusal));
        }
    }

    /// <summary>
    /// The scan looks at files. If it ever looks at none, both register legs
    /// pass for the wrong reason, so it says so instead.
    /// </summary>
    [Fact]
    public void TheScanActuallyReadsThePluginSources()
    {
        Assert.NotEmpty(PluginSourceFiles());
    }

    /// <summary>
    /// Returns the site string for the place inside the plugin that a refusal
    /// was thrown from, in the same spelling the scan produces.
    /// </summary>
    /// <remarks>
    /// The frames are read outermost-throw first, and the first one carrying a
    /// file inside the plugin is the answer: the helpers that throw on a
    /// caller's behalf live in the framework and carry no file at all, so the
    /// first file-bearing frame is the line somebody wrote. It is read out of
    /// the source rather than trusted from the entry, so a guard that moved to
    /// another line reports the line it moved to.
    /// </remarks>
    private static string SiteThatRefused(Exception refusal)
    {
        var root = Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.MetadataSync");

        foreach (var frame in new StackTrace(refusal, true).GetFrames())
        {
            var file = frame.GetFileName();
            var line = frame.GetFileLineNumber();

            if (file is null || line <= 0 || !IsUnder(root, file))
            {
                continue;
            }

            var text = File.ReadLines(file).Skip(line - 1).First().Trim();
            return $"{RelativeTo(root, file)} -> {text}";
        }

        // Every frame with a file was outside the plugin, or none carried one.
        // The second case is a build without line information rather than a
        // refusal from somewhere else, and the caller's assertion says which by
        // printing the site it wanted.
        return "no frame inside the plugin carried a source line";
    }

    private static bool IsUnder(string root, string file)
    {
        return !Path.GetRelativePath(root, file).StartsWith("..", StringComparison.Ordinal);
    }

    private static void AssertIsAFact(string testClass, string testMethod)
    {
        var type = typeof(RefusalTests).Assembly.GetType(
            $"{typeof(RefusalTests).Namespace}.{testClass}");
        Assert.NotNull(type);

        var method = type.GetMethod(testMethod, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        Assert.NotEmpty(method.GetCustomAttributes(typeof(FactAttribute), false));
    }

    /// <summary>
    /// Reads the plugin sources and returns one entry per line of code that
    /// refuses. Comment lines are skipped, because a comment describing a
    /// refusal is not one.
    /// </summary>
    /// <remarks>
    /// The bound is honest: this is a line scan and not a parse. A throw
    /// spelled across two lines is missed, and the word inside a block comment
    /// or a string literal is counted. It reads the two spellings this
    /// repository uses, which are the <c>throw</c> keyword and the argument
    /// helpers that throw on the caller's behalf.
    /// </remarks>
    private static IReadOnlyList<string> RefusalSites()
    {
        var root = Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.MetadataSync");

        return PluginSourceFiles()
            .SelectMany(file => RefusalLines(file)
                .Select(line => $"{RelativeTo(root, file)} -> {line}"))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> RefusalLines(string file)
    {
        return File.ReadLines(file)
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
            .Where(line => !line.StartsWith('*'))
            .Where(line => line.Contains("throw ", StringComparison.Ordinal)
                || line.Contains(".Throw", StringComparison.Ordinal));
    }

    private static string RelativeTo(string root, string file)
    {
        return Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static IReadOnlyList<string> PluginSourceFiles()
    {
        var pluginDirectory = Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.MetadataSync");
        Assert.True(Directory.Exists(pluginDirectory), $"Plugin sources not found at {pluginDirectory}");

        return Directory.GetFiles(pluginDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "")
    {
        // This file sits one directory below the repository root, and the
        // compiler writes its path in. Walking up from the test binary instead
        // would depend on the configuration and the target framework.
        var testProjectDirectory = Path.GetDirectoryName(thisFile);
        Assert.NotNull(testProjectDirectory);

        var root = Path.GetDirectoryName(testProjectDirectory);
        Assert.NotNull(root);
        return root;
    }
}
