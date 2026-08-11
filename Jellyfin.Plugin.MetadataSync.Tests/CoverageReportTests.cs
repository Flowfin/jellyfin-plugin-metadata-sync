using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the coverage route up: what the collector is pointed at, what the run
/// does with the report afterwards, and the bar the decision code is held to.
///
/// Every leg below except the last runs against a constructed report, so the
/// refusals are proved on this machine and on every machine, rather than only on
/// the day a real run happens to be broken. The reports are written out in full
/// rather than built by a helper that hides the attribute under test, because the
/// attribute under test is usually the one that is missing.
///
/// The last leg is the real one and it needs a report to read. It reads the
/// directory out of the environment, which the coverage workflow sets, and on an
/// ordinary run there is nothing there and it passes having read nothing. That
/// hole is why <see cref="TheCoverageWorkflowCollectsAndThenReadsTheReport"/>
/// exists: the route that judges the bar is the workflow, so the workflow file
/// is read here and a workflow that stopped collecting or stopped reading is
/// refused.
/// </summary>
public class CoverageReportTests
{
    /// <summary>
    /// The shape the collector writes when its module filter matched nothing. It
    /// is a valid report, it claims perfect rates, and it carries no count at
    /// all. Measured rather than imagined: this is what a run with a Sources
    /// filter that matched no path produced while #74 was being worked.
    /// </summary>
    private const string MeasuredNothing =
        "<coverage line-rate=\"1\" branch-rate=\"1\" complexity=\"1\" version=\"1.9\" timestamp=\"1786354302\" />";

    [Fact]
    public void AReportThatMeasuredNothingIsRefused()
    {
        var reading = CoverageReport.Read(MeasuredNothing);

        // The wording is asserted rather than the word, because the arm below
        // this one in the reader also says "lines-valid": a report carrying the
        // attribute set to nothing lands there. Asserting the shared word left
        // this leg green with the arm it is about deleted, which is how that was
        // found.
        Assert.Contains(reading.Refusals, r => r.Contains("states no lines-valid", StringComparison.Ordinal));
        Assert.Empty(reading.Areas);
    }

    [Fact]
    public void AReportStatingNoMeasurableLineIsRefused()
    {
        var reading = CoverageReport.Read(
            "<coverage lines-covered=\"0\" lines-valid=\"0\" branches-covered=\"0\" branches-valid=\"0\" />");

        Assert.Contains(reading.Refusals, r => r.Contains("nothing was measured", StringComparison.Ordinal));
    }

    [Fact]
    public void SomethingThatIsNotAReportIsRefused()
    {
        var reading = CoverageReport.Read("vstest.console process failed to connect to datacollector process");

        Assert.Contains(reading.Refusals, r => r.Contains("not readable as XML", StringComparison.Ordinal));
    }

    [Fact]
    public void AReportNamingNoClassIsRefused()
    {
        var reading = CoverageReport.Read(
            "<coverage lines-covered=\"9\" lines-valid=\"9\" branches-covered=\"0\" branches-valid=\"0\"><packages /></coverage>");

        Assert.Contains(reading.Refusals, r => r.Contains("names no class", StringComparison.Ordinal));
    }

    /// <summary>
    /// A namespace the plugin grows later has to be assigned an area on purpose.
    /// The alternative is that it lands in whichever area matched first and the
    /// figure for the decision code silently starts answering for something else.
    /// </summary>
    [Fact]
    public void AClassNoAreaClaimsIsRefused()
    {
        var reading = CoverageReport.Read(ReportWith(
            statedLines: 1,
            statedBranches: 0,
            Measured("Jellyfin.Plugin.MetadataSync.Reconcile.Planner", ("11", 0, null))));

        Assert.Contains(reading.Refusals, r => r.Contains("No area claims", StringComparison.Ordinal));
        Assert.Contains(reading.Refusals, r => r.Contains("Reconcile.Planner", StringComparison.Ordinal));
    }

    /// <summary>
    /// The areas are a partition of what the run measured, and the report states
    /// its own totals, so the two are compared. A class from another module
    /// arriving through a widened filter shows up here as a disagreement rather
    /// than as a better number.
    /// </summary>
    [Fact]
    public void AReadingThatDoesNotAccountForEveryMeasuredLineIsRefused()
    {
        var reading = CoverageReport.Read(ReportWith(
            statedLines: 40,
            statedBranches: 0,
            Measured("Jellyfin.Plugin.MetadataSync.Conflicts.ConflictResolver", ("11", 3, null))));

        Assert.Contains(reading.Refusals, r => r.Contains("account for", StringComparison.Ordinal));
    }

    /// <summary>
    /// One figure for the whole repository is the shape that lets a drop in the
    /// decision code hide behind a rise elsewhere, so the two areas are counted
    /// apart and an uncovered entry point cannot move the decision figure.
    /// </summary>
    [Fact]
    public void TheDecisionCodeAndTheEntryPointAreCountedApart()
    {
        var reading = CoverageReport.Read(ReportWith(
            statedLines: 3,
            statedBranches: 2,
            Measured("Jellyfin.Plugin.MetadataSync.Conflicts.ConflictResolver", ("40", 7, "100% (2/2)")),
            Measured("Jellyfin.Plugin.MetadataSync.Plugin", ("23", 0, null), ("24", 0, null))));

        Assert.Empty(reading.Refusals);

        var decision = reading.Areas.Single(a => a.HeldToTheBar);
        Assert.Equal("Decision code", decision.Name);
        Assert.Equal(1, decision.LinesCovered);
        Assert.Equal(1, decision.LinesValid);
        Assert.Equal(2, decision.BranchesCovered);
        Assert.Equal(2, decision.BranchesValid);

        var entry = reading.Areas.Single(a => !a.HeldToTheBar);
        Assert.Equal(0, entry.LinesCovered);
        Assert.Equal(2, entry.LinesValid);
    }

    /// <summary>
    /// A compiler-generated lambda class carries the namespace of the type that
    /// declared it, and the branches inside a lambda are exactly where a rule's
    /// second condition goes unreached. It is claimed by the same area rather
    /// than by none.
    /// </summary>
    [Fact]
    public void ALambdaClassIsClaimedByTheAreaOfTheTypeThatDeclaredIt()
    {
        var reading = CoverageReport.Read(ReportWith(
            statedLines: 1,
            statedBranches: 2,
            Measured("Jellyfin.Plugin.MetadataSync.Conflicts.ConflictResolver.&lt;&gt;c", ("75", 4, "50% (1/2)"))));

        Assert.Empty(reading.Refusals);
        Assert.Equal(2, reading.Areas.Single(a => a.HeldToTheBar).BranchesValid);
    }

    [Fact]
    public void AnUnreachedBranchInTheDecisionCodeIsNamedWithItsLine()
    {
        var reading = CoverageReport.Read(ReportWith(
            statedLines: 2,
            statedBranches: 2,
            Measured(
                "Jellyfin.Plugin.MetadataSync.Fields.FieldMover",
                ("86", 12, "50% (1/2)"),
                ("110", 0, null))));

        Assert.Empty(reading.Refusals);
        Assert.Contains("Fields.FieldMover line 86, 1 of 2 branch(es) reached", reading.Unreached);
        Assert.Contains("Fields.FieldMover line 110, never executed", reading.Unreached);
    }

    /// <summary>
    /// The entry point and the registration are not held to the bar, so a line
    /// nothing reaches there is reported in the figures and is not named as a
    /// gap. Covering them means asserting that a call was made, and a test that
    /// asserts a call was made mostly tests itself.
    /// </summary>
    [Fact]
    public void AnUnreachedLineOutsideTheHeldAreasIsNotNamedAsAGap()
    {
        var reading = CoverageReport.Read(ReportWith(
            statedLines: 1,
            statedBranches: 0,
            Measured("Jellyfin.Plugin.MetadataSync.PluginServiceRegistrator.&lt;&gt;c", ("22", 0, null))));

        Assert.Empty(reading.Refusals);
        Assert.Empty(reading.Unreached);
    }

    [Fact]
    public void TheRenderedReportCarriesOneRowPerAreaAndNamesEveryGap()
    {
        var reading = CoverageReport.Read(ReportWith(
            statedLines: 2,
            statedBranches: 0,
            Measured("Jellyfin.Plugin.MetadataSync.Matching.ProviderIdentifiers", ("141", 0, null)),
            Measured("Jellyfin.Plugin.MetadataSync.Plugin", ("23", 0, null))));

        var report = CoverageReport.Render(reading);

        Assert.Contains("| Decision code | yes | 0/1 |", report, StringComparison.Ordinal);
        Assert.Contains("| Entry point and registration | no | 0/1 |", report, StringComparison.Ordinal);
        Assert.Contains("Matching.ProviderIdentifiers line 141, never executed", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// The run settings are the difference between measuring the plugin and
    /// measuring the suite along with it. A filter removed there does not fail
    /// anything by itself: it widens the denominator with test code, which drifts
    /// with the suite instead of with the decisions, so the file is held to both
    /// choices it exists to make.
    /// </summary>
    [Fact]
    public void TheRunSettingsMeasureThePluginModuleAndAskForCobertura()
    {
        var settings = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "coverage.runsettings"));

        var format = settings.Descendants("Format").Single().Value;
        Assert.Equal("cobertura", format, StringComparer.Ordinal);

        var included = settings.Descendants("ModulePath").Select(m => m.Value).ToList();
        Assert.Single(included);
        Assert.Contains("Jellyfin", included[0], StringComparison.Ordinal);
        Assert.Contains("MetadataSync", included[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Tests", included[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// The route that judges the bar is the coverage workflow: it collects with
    /// the settings above and then runs the reading with the directory in the
    /// environment. Without both halves the last leg of this class reads nothing
    /// and passes, so both halves are read out of the workflow file here.
    ///
    /// Its bound: this is a text scan over YAML and it matches spellings. It says
    /// the workflow names the settings file, the variable and the filter. It does
    /// not say the steps run in that order, and it does not say the run succeeded.
    /// The run itself is what says that.
    /// </summary>
    [Fact]
    public void TheCoverageWorkflowCollectsAndThenReadsTheReport()
    {
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "coverage.yml"));

        Assert.Contains("--settings coverage.runsettings", workflow, StringComparison.Ordinal);
        Assert.Contains(CoverageReport.ReportDirectoryVariable, workflow, StringComparison.Ordinal);
        Assert.Contains(nameof(CoverageReportTests), workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// The bar in #74: every line and every branch in the decision code is
    /// reached, and a run that could not collect fails rather than reporting
    /// nothing.
    ///
    /// On a run with no report directory in the environment this reads nothing and
    /// passes. That is stated rather than hidden, and it is the reason the leg
    /// above reads the workflow file. What this leg proves, when it has a report,
    /// is that the collector produced one the reading accepts and that the areas
    /// held to the bar are whole.
    /// </summary>
    [Fact]
    public void TheDecisionCodeHasNoUnreachedLineOrBranch()
    {
        var directory = Environment.GetEnvironmentVariable(CoverageReport.ReportDirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Assert.True(
            Directory.Exists(directory),
            CoverageReport.ReportDirectoryVariable + " names '" + directory + "', which is not a directory, so no run collected anything there.");

        var reports = Directory.GetFiles(directory, "*.cobertura.xml", SearchOption.AllDirectories);
        Assert.True(
            reports.Length == 1,
            string.Format(
                CultureInfo.InvariantCulture,
                "Expected exactly one cobertura report under '{0}' and found {1}. Reading one of several would answer a different question, because the others are earlier runs.",
                directory,
                reports.Length));

        var reading = CoverageReport.Read(File.ReadAllText(reports[0]));
        var report = CoverageReport.Render(reading);
        File.WriteAllText(Path.Combine(directory, CoverageReport.RenderedReportName), report);

        Assert.True(reading.Refusals.Count == 0, "The reading is refused: " + string.Join("; ", reading.Refusals));
        Assert.True(
            reading.Unreached.Count == 0,
            "The decision code carries " + reading.Unreached.Count.ToString(CultureInfo.InvariantCulture)
                + " line(s) nothing reached: " + string.Join("; ", reading.Unreached));

        foreach (var area in reading.Areas)
        {
            Assert.True(
                area.LinesValid > 0,
                "The area '" + area.Name + "' measured no line at all, so its figure says nothing.");
        }
    }

    private static string Measured(string typeName, params (string Number, int Hits, string? Condition)[] lines)
    {
        var written = lines.Select(l => string.Format(
            CultureInfo.InvariantCulture,
            "<line number=\"{0}\" hits=\"{1}\" branch=\"{2}\"{3} />",
            l.Number,
            l.Hits,
            l.Condition is null ? "False" : "True",
            l.Condition is null ? string.Empty : " condition-coverage=\"" + l.Condition + "\""));

        // The lines are written under a method, which is where the collector puts
        // them and where the counts add up to the totals the report states about
        // itself. A line reached from two methods is listed once at class level,
        // so a reading taken from there would sit under the stated total.
        return "<class name=\"" + typeName + "\" filename=\"whatever.cs\" line-rate=\"0\" branch-rate=\"0\" complexity=\"1\">"
            + "<methods><method name=\"M\" signature=\"()\" line-rate=\"0\" branch-rate=\"0\"><lines>"
            + string.Join(string.Empty, written)
            + "</lines></method></methods>"
            + "<lines>" + string.Join(string.Empty, written) + "</lines>"
            + "</class>";
    }

    private static string ReportWith(int statedLines, int statedBranches, params string[] classes)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "<coverage line-rate=\"0\" branch-rate=\"0\" complexity=\"1\" version=\"1.9\" timestamp=\"1786468765\" lines-covered=\"0\" lines-valid=\"{0}\" branches-covered=\"0\" branches-valid=\"{1}\"><packages><package name=\"Jellyfin.Plugin.MetadataSync\"><classes>{2}</classes></package></packages></coverage>",
            statedLines,
            statedBranches,
            string.Join(string.Empty, classes));
    }
}
