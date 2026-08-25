using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MetadataSync.Reconciliation;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The applier, which is supposed to be dull, held to being dull.
/// </summary>
/// <remarks>
/// Two things are asserted here and they are different in kind. What the
/// applier does is observed through the one interface it holds, so a call it
/// made and a call it did not make are both visible. What the applier is
/// allowed to know is read off its source text, because no arrangement of
/// inputs can prove that a type never consults the field register: an applier
/// that read a rule and happened to agree with the plan would pass every
/// behavioural test in this file.
/// <para>
/// The source scan is a line scan and not a parse, so it matches a spelling.
/// It cannot catch a rule reached under another name, a rule read through an
/// injected interface, or a decision copied into the applier as an inline
/// literal that happens to agree with the table. What it does catch is the
/// edit somebody actually makes, which is reaching for the register at the
/// moment the plan turns out not to carry something.
/// </para>
/// </remarks>
public class ApplierTests
{
    /// <summary>
    /// The names the applier may not read. Each one is an authority that
    /// answered while the plan was being made, and reading it again is how the
    /// two halves of a pass come to disagree about the same field.
    /// </summary>
    private static readonly string[] ForbiddenNames =
    {
        "FieldRegister",
        "FieldRow",
        "SyncDirection",
        "Direction",
        "ConflictResolver",
        "ConflictRules",
        "ConflictRule",
        "ConflictInputs",
        "ConflictOutcome",
        "ConflictDecision",
        "PlanDisposition",
    };

    /// <summary>
    /// Gets the names the source scan refuses, so the theory below names one
    /// rather than reporting them all in one failure.
    /// </summary>
    public static TheoryData<string> Forbidden
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var name in ForbiddenNames)
            {
                names.Add(name);
            }

            return names;
        }
    }

    /// <summary>
    /// The condition read literally: an applier given a plan with nothing in it
    /// calls nothing.
    /// </summary>
    [Fact]
    public async Task AnEmptyPlanReachesTheLibraryNotAtAll()
    {
        var target = new RecordingPlanTarget();

        var result = await new Applier(target, new RecordingWrittenValues()).ApplyAsync(new Plan(), CancellationToken.None);

        Assert.Empty(target.Written);
        Assert.Equal(0, result.ItemsWritten);
        Assert.Equal(0, result.FieldsWritten);
        Assert.Equal(0, result.ItemsPassedOver);
    }

    /// <summary>
    /// A plan with items in it that writes nothing calls nothing either, which
    /// is the property that makes a dry run provably a dry run. This is the
    /// case the test above cannot cover: an empty plan is trivially quiet, and
    /// a plan over ten thousand items that decided against every one of them is
    /// the one an operator actually has.
    /// </summary>
    [Fact]
    public async Task APlanThatWritesNothingReachesTheLibraryNotAtAll()
    {
        var target = new RecordingPlanTarget();
        var plan = PlanOf(ItemThat(writes: false), ItemThat(writes: false));

        var result = await new Applier(target, new RecordingWrittenValues()).ApplyAsync(plan, CancellationToken.None);

        Assert.Empty(target.Written);
        Assert.Equal(0, result.ItemsWritten);
        Assert.Equal(2, result.ItemsPassedOver);
    }

    /// <summary>
    /// The neighbour. One item that writes is one call, so the two tests above
    /// are not passing against an applier that calls nothing ever.
    /// </summary>
    [Fact]
    public async Task AnItemThatWritesIsHandedToTheTargetExactlyOnce()
    {
        var target = new RecordingPlanTarget();
        var writing = ItemThat(writes: true);
        var plan = PlanOf(ItemThat(writes: false), writing);

        var result = await new Applier(target, new RecordingWrittenValues()).ApplyAsync(plan, CancellationToken.None);

        Assert.Same(writing, Assert.Single(target.Written));
        Assert.Equal(1, result.ItemsWritten);
        Assert.Equal(1, result.FieldsWritten);
        Assert.Equal(1, result.ItemsPassedOver);
    }

    /// <summary>
    /// The token an operator stops a pass with reaches the target. The applier
    /// does not decide what stopping means, which is the pass's own bound and
    /// is #37, but a token it dropped could not be honoured by anything
    /// downstream either.
    /// </summary>
    [Fact]
    public async Task TheTokenReachesTheTarget()
    {
        using var stopping = new CancellationTokenSource();
        var target = new RecordingPlanTarget();

        await new Applier(target, new RecordingWrittenValues()).ApplyAsync(PlanOf(ItemThat(writes: true)), stopping.Token);

        Assert.Equal(stopping.Token, Assert.Single(target.Tokens));
    }

    /// <summary>
    /// A pass an operator stopped stops within one item. The token reaching the
    /// target is not enough on its own: the applier decides when to call the
    /// target at all, so a token it only handed on would leave a stopped pass
    /// starting work on every remaining item in the plan.
    /// </summary>
    /// <remarks>
    /// The number is one and it is stated rather than implied. The plan here
    /// carries three items that write and the token is already cancelled, so an
    /// applier that stopped at the end would hand over three, one that checked
    /// after each item would hand over one, and this one hands over none. What
    /// is asserted is the bound rather than the exact count, because an applier
    /// that finishes the item it is inside is within the same promise.
    /// </remarks>
    [Fact]
    public async Task ACancelledPassStopsWithinOneItem()
    {
        var target = new RecordingPlanTarget();
        var plan = PlanOf(ItemThat(writes: true), ItemThat(writes: true), ItemThat(writes: true));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new Applier(target, new RecordingWrittenValues()).ApplyAsync(plan, new CancellationToken(canceled: true)));

        Assert.True(target.Written.Count <= 1, "A stopped pass handed over " + target.Written.Count + " items.");
    }

    /// <summary>
    /// An applier with no route to a library is refused when it is built rather
    /// than when it is first used, because an applier that exists and cannot
    /// write is a pass that reports having applied a plan.
    /// </summary>
    [Fact]
    public void AnApplierWithNoTargetIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new Applier(null!, new RecordingWrittenValues()));
    }

    /// <summary>
    /// Applying nothing is refused rather than treated as applying an empty
    /// plan, for the same reason: the caller that lost its plan and the caller
    /// that has nothing to do are different situations.
    /// </summary>
    [Fact]
    public async Task ApplyingAPlanThatIsNotThereIsRefused()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new Applier(new RecordingPlanTarget(), new RecordingWrittenValues()).ApplyAsync(null!, CancellationToken.None));
    }

    /// <summary>
    /// The applier's source names none of the authorities the planner asked.
    /// This is the condition that it contains no conditional reading the field
    /// register, the direction model or the conflict rules, decided by a scan
    /// rather than by review.
    /// </summary>
    /// <param name="name">The name the applier may not read.</param>
    [Theory]
    [MemberData(nameof(Forbidden))]
    public void TheApplierNamesNoneOfTheAuthoritiesThePlannerAsked(string name)
    {
        var findings = Lines(ApplierSource())
            .Where(line => line.Text.Contains(name, StringComparison.Ordinal))
            .Select(line => line.Number + ": " + line.Text)
            .ToList();

        Assert.True(findings.Count == 0, "Applier.cs names " + name + " at " + string.Join("; ", findings));
    }

    /// <summary>
    /// The scan reads the applier. A source scan over a file it failed to find
    /// passes every rule above, so the file is asserted before anything is
    /// concluded from a clean run, and so is the fact that the scan can see a
    /// name that is genuinely there.
    /// </summary>
    [Fact]
    public void TheScanReadsTheApplierAndWouldSeeAForbiddenName()
    {
        var lines = Lines(ApplierSource());

        Assert.NotEmpty(lines);
        Assert.Contains(lines, line => line.Text.Contains("IPlanTarget", StringComparison.Ordinal));
    }

    /// <summary>
    /// The planner, by contrast, does name them. Without this the scan above
    /// could be passing because the names are spelled differently everywhere,
    /// rather than because the applier does not read them.
    /// </summary>
    [Fact]
    public void ThePlannerNamesTheAuthoritiesTheApplierMayNot()
    {
        var planner = File.ReadAllText(Path.Combine(ReconciliationDirectory(), "Planner.cs"));

        Assert.Contains("FieldRegister", planner, StringComparison.Ordinal);
        Assert.Contains("ConflictResolver", planner, StringComparison.Ordinal);
    }

    private static string ApplierSource() => File.ReadAllText(Path.Combine(ReconciliationDirectory(), "Applier.cs"));

    /// <summary>
    /// The applier's lines, with the comments taken out. A comment explaining
    /// what the applier may not read is not the applier reading it, and the
    /// remark above says what that costs.
    /// </summary>
    private static IReadOnlyList<(int Number, string Text)> Lines(string source)
    {
        var lines = new List<(int Number, string Text)>();
        var number = 0;

        foreach (var raw in source.Split('\n'))
        {
            number++;
            var text = raw.TrimEnd('\r').Trim();

            if (text.StartsWith("//", StringComparison.Ordinal)
                || text.StartsWith("///", StringComparison.Ordinal)
                || text.StartsWith('*')
                || text.StartsWith("/*", StringComparison.Ordinal))
            {
                continue;
            }

            lines.Add((number, text));
        }

        return lines;
    }

    private static Plan PlanOf(params ItemPlan[] items)
    {
        var plan = new Plan();
        foreach (var item in items)
        {
            plan.Items.Add(item);
        }

        return plan;
    }

    private static ItemPlan ItemThat(bool writes)
    {
        var item = new ItemPlan { Kind = "Movie" };

        item.Changes.Add(new PlannedChange
        {
            Field = "Overview",
            PeerValue = "theirs",
            Writes = writes,
            ValueToWrite = writes ? "theirs" : null,
        });

        return item;
    }

    private static string ReconciliationDirectory()
    {
        return Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.MetadataSync", "Reconciliation");
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "")
    {
        var testProjectDirectory = Path.GetDirectoryName(thisFile);
        Assert.NotNull(testProjectDirectory);

        var root = Path.GetDirectoryName(testProjectDirectory);
        Assert.NotNull(root);
        return root;
    }
}
