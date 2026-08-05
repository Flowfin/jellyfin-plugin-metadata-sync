using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Keeps the static plugin instance where the plugin base class forces it to
/// be, and nowhere else. A service that reaches Plugin.Instance cannot be given
/// a different configuration in a test, so this fails on the first new reader
/// rather than after the untestable code is written.
/// </summary>
public class StaticInstanceTests
{
    /// <summary>
    /// The files allowed to read the static, and why each one is here. Adding a
    /// name to this set is a decision somebody makes on purpose, which is the
    /// point of writing it down rather than counting occurrences.
    /// </summary>
    private static readonly HashSet<string> AllowedReaders = new(StringComparer.Ordinal)
    {
        // The plugin entry point assigns it; the base class gives no other way.
        "Plugin.cs",

        // The registration point closes over it once, so that everything it
        // registers is handed its configuration instead of fetching it.
        "PluginServiceRegistrator.cs",
    };

    /// <summary>
    /// No source file outside the allowed set reads the static instance.
    /// </summary>
    [Fact]
    public void OnlyTheEntryPointAndTheRegistratorReadTheStaticInstance()
    {
        var offenders = PluginSourceFiles()
            .Where(ReadsTheStaticInstance)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !AllowedReaders.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The scan looks at files. If it ever looks at none, it passes for the
    /// wrong reason, so it says so instead.
    /// </summary>
    [Fact]
    public void TheScanActuallyReadsThePluginSources()
    {
        Assert.NotEmpty(PluginSourceFiles());
    }

    /// <summary>
    /// Reads a file and asks whether any line of code names the static. Comment
    /// lines are skipped, because a comment explaining the rule is not a
    /// violation of it. The bound is honest: this is a line scan and not a
    /// parse, so a mention inside a block comment or a string literal would be
    /// counted, and a read spread across two lines would not.
    /// </summary>
    private static bool ReadsTheStaticInstance(string file)
    {
        return File.ReadLines(file)
            .Select(line => line.TrimStart())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
            .Where(line => !line.StartsWith("*", StringComparison.Ordinal))
            .Any(line => line.Contains("Plugin.Instance", StringComparison.Ordinal));
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
