using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Every place the plugin refuses is named here, against the test that trips it
/// and the neighbour that differs by one thing and is not refused. A refusal
/// nobody trips has never been seen to happen, and it is indistinguishable from
/// a line that cannot be reached at all.
/// </summary>
/// <remarks>
/// This class holds no trip test of its own. The tests it names live beside the
/// type they are about, and a second copy of an assertion is a copy that
/// drifts. What is added here is the register, which is the part that keeps
/// working after it is written: a guard added to the plugin later with no entry
/// fails the suite rather than waiting for somebody to notice it in review, and
/// a guard deleted while its entry stays fails it the same way. Both
/// directions, because a register that only grows stops describing the tree the
/// moment a guard moves.
/// </remarks>
public class RefusalTests
{
    /// <summary>
    /// One entry per refusal site the plugin carries. The key is the file the
    /// site is in and the line of code that refuses, so an edit to the guard
    /// itself shows up here rather than passing under a line number that
    /// happens to still match.
    /// </summary>
    private static readonly Dictionary<string, (string TestClass, string Trip, string Neighbour, string DiffersBy)> Register =
        new(StringComparer.Ordinal)
        {
            ["Configuration/PluginConfigurationProvider.cs -> ArgumentNullException.ThrowIfNull(read);"] =
                (nameof(ServiceRegistrationTests),
                 nameof(ServiceRegistrationTests.ConfigurationProviderRefusesAMissingReader),
                 nameof(ServiceRegistrationTests.ConfigurationProviderReturnsTheConfigurationItWasGiven),
                 "the delegate is there"),
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
    /// What this does not do is prove that the named trip executes the site it
    /// is registered against, or that the neighbour differs from it by the one
    /// thing the entry says. Nothing here reads coverage, so both pairings are
    /// claims made when the entry is written. The bound is stated rather than
    /// left for a reader to discover.
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
    /// The scan looks at files. If it ever looks at none, both register legs
    /// pass for the wrong reason, so it says so instead.
    /// </summary>
    [Fact]
    public void TheScanActuallyReadsThePluginSources()
    {
        Assert.NotEmpty(PluginSourceFiles());
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
