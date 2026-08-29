using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The server starts nothing in this plugin on its own, so a plugin an operator
/// has disabled runs no pass.
/// </summary>
/// <remarks>
/// #62 asks for a test that a disabled plugin runs no pass and deletes nothing.
/// The second half is held already, by the walk over the plugin assembly for the
/// library's delete surface that #66's first condition is met on. This file is
/// the first half, and it answers it in the form the tree allows rather than in
/// the form the condition imagines, which is worth stating rather than reading
/// past.
/// <para>
/// The condition imagines a plugin that runs a pass when it is enabled, and a
/// case that switches it off and watches nothing happen. There is nothing here
/// that runs a pass in either state: this plugin contributes no work the server
/// would invoke, so the question of what disabling changes has no subject yet.
/// What is asserted instead is that absence, held rather than assumed, which is
/// the shape <c>docs/lifecycle.md</c> already uses for the members this plugin
/// takes over from the server's plugin base.
/// </para>
/// <para>
/// THE DAY THIS REDDENS IS THE POINT OF IT. A scheduled task is #40, and the
/// first thing that registers one turns this red. That is not an obstacle to
/// #40: it is where the disabled-plugin question stops being vacuous and has to
/// be answered, and a guard that quietly kept passing through that change would
/// leave #62 reported as met about a plugin the server had started running.
/// </para>
/// <para>
/// It is derived rather than listed. What is compared is every contract this
/// plugin takes on that the server declares, found by walking the assembly, so
/// a contract nobody thought to name here is refused along with the ones that
/// were. A named check for the scheduled task interface sits beside it, because
/// that is the one the condition is actually about and a set comparison that
/// happened to be empty for some other reason would not say so.
/// </para>
/// <para>
/// What it does not reach. Whether a server runs a disabled plugin's registered
/// work at all is a property of the server and not of this tree, and nothing
/// here starts one, which is the headless policy in <c>docs/testing.md</c>. It
/// reads contracts a type declares, so work reached some other way - a timer
/// started inside a constructor, a thread - spells none of these and is not
/// refused here. Nothing in this plugin does that today, and that sentence is a
/// reading rather than a guard.
/// </para>
/// </remarks>
public class SelfStartingWorkTests
{
    /// <summary>
    /// The contracts the server declares that this plugin takes on, each with
    /// the reason it is here. Every one of them is answered when the server
    /// asks, and none of them is a way for the server to begin work.
    /// </summary>
    private static readonly string[] Declared =
    {
        // The plugin's page, read out of the plugin when the dashboard asks it
        // what pages it has.
        "MediaBrowser.Model.Plugins.IHasWebPages",

        // The registration of this plugin's own services into the container, at
        // the moment the plugin is loaded.
        "MediaBrowser.Controller.Plugins.IPluginServiceRegistrator",

        // The three that arrive with the server's plugin base rather than being
        // chosen: what the plugin is called and which identifier it has, the
        // configuration it carries, and where its assembly was loaded from.
        // They are here because they are taken on and not because they were
        // wanted, and each is answered when the server asks. None of them is a
        // way for the server to begin work.
        "MediaBrowser.Common.Plugins.IPlugin",
        "MediaBrowser.Common.Plugins.IHasPluginConfiguration",
        "MediaBrowser.Common.Plugins.IPluginAssembly",
    };

    /// <summary>
    /// Every contract this plugin takes on is one of those above, in both
    /// directions. One arriving reddens here whether or not anybody remembered
    /// this file, and one disappearing reddens too, because a set that only
    /// grows would pass on a plugin that had stopped declaring its page.
    /// </summary>
    [Fact]
    public void TheOnlyServerContractsThisPluginTakesOnAreTheOnesItDeclares()
    {
        Assert.Equal(
            Declared.Order().ToList(),
            ServerContractsTakenOnBy(typeof(Plugin).Assembly).Order().ToList());
    }

    /// <summary>
    /// Nothing here is the server's scheduled work. This is the named half, and
    /// it is separate from the set above on purpose: the set would be satisfied
    /// by a plugin that declared no contracts at all, and this says which
    /// contract in particular is absent.
    /// </summary>
    [Fact]
    public void NothingHereIsScheduledWorkTheServerStarts()
    {
        Assert.Empty(
            typeof(Plugin).Assembly.GetTypes()
                .Where(type => typeof(IScheduledTask).IsAssignableFrom(type))
                .Select(type => type.FullName ?? type.Name));
    }

    /// <summary>
    /// The reader finds one when there is one to find. Without this leg both
    /// cases above pass on a reader that returns nothing, which is the failure
    /// an absence guard reaches for first.
    /// </summary>
    [Fact]
    public void TheReaderFindsAContractWhereOneIsDeclared()
    {
        var found = ServerContractsTakenOnBy(typeof(SelfStartingWorkTests).Assembly);

        Assert.Contains("MediaBrowser.Model.Tasks.IScheduledTask", found);
    }

    /// <summary>
    /// Every name in the declared set is a type that exists. A row misspelled
    /// here would be a contract nobody could take on, so the set would agree
    /// with the tree for the wrong reason.
    /// </summary>
    [Fact]
    public void EveryNameInTheDeclaredSetIsOneThatExists()
    {
        var known = typeof(Plugin).Assembly.GetTypes()
            .SelectMany(type => type.GetInterfaces())
            .Concat(typeof(IScheduledTask).Assembly.GetTypes())
            .Select(type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in Declared)
        {
            Assert.Contains(name, known);
        }
    }

    /// <summary>
    /// Every interface an assembly's types take on that the server declares.
    /// </summary>
    /// <remarks>
    /// The server's is decided by where the interface lives rather than by a
    /// list of names, so a contract in a namespace nobody here has heard of is
    /// still read as the server's. This plugin's own interfaces and the runtime
    /// library's are not contracts with a host, which is why they are not part
    /// of the question.
    /// </remarks>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The full names of the contracts, deduplicated.</returns>
    private static IEnumerable<string> ServerContractsTakenOnBy(System.Reflection.Assembly assembly) =>
        assembly.GetTypes()
            .SelectMany(type => type.GetInterfaces())
            .Where(IsDeclaredByTheServer)
            .Select(type => type.FullName ?? type.Name)
            .Distinct(StringComparer.Ordinal);

    private static bool IsDeclaredByTheServer(Type contract)
    {
        var declaringAssembly = contract.Assembly.GetName().Name ?? string.Empty;

        return declaringAssembly.StartsWith("MediaBrowser.", StringComparison.Ordinal)
            || (declaringAssembly.StartsWith("Jellyfin.", StringComparison.Ordinal)
                && !declaringAssembly.StartsWith("Jellyfin.Plugin.MetadataSync", StringComparison.Ordinal));
    }

    /// <summary>
    /// A type that takes on the contract the server starts work through, so the
    /// reader has one to find. It is in the suite and never in the plugin, and
    /// nothing runs it: what it exists for is to be seen by a walk.
    /// </summary>
    private sealed class WorkTheServerWouldStart : IScheduledTask
    {
        public string Name => "Never registered anywhere.";

        public string Key => "metadata-sync-fixture-never-registered";

        public string Description => "A fixture that exists so an absence guard can be shown to find a presence.";

        public string Category => "Fixture";

        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This fixture is read by a walk and never run.");

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();
    }
}
