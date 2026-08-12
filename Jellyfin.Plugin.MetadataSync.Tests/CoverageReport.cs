using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Reads one cobertura report and answers it per area, with the decision code as
/// its own area.
/// </summary>
/// <remarks>
/// A single repository-wide percentage is the shape that lets a drop in the
/// decision code hide behind a rise elsewhere, so the figure is never one
/// figure. The areas are declared in <see cref="Areas"/> and every class the run
/// measured has to fall in one of them, which is what makes a namespace nobody
/// assigned a red run rather than a silent addition to whichever area sorted
/// first.
///
/// It reports refusals as values rather than throwing them, so each one is a
/// thing a test compares against a constructed report instead of an exception
/// type. The refusal that matters most is the empty read: the collector answers
/// a filter that matches nothing with a report carrying no lines-valid attribute
/// at all, and a run that collected nothing then looks exactly like a run that
/// covered everything.
///
/// The counts are taken from the lines under each method rather than the lines
/// under each class. The two differ, because a line reached by two methods is
/// listed once at class level, and only the method-level enumeration reproduces
/// the totals the report states about itself. Those totals are then compared
/// against the sum of the areas, so a class outside the plugin module cannot
/// arrive unnoticed.
///
/// What it cannot do: say whether a covered branch was covered by an assertion
/// worth making. It counts what the collector executed, which is why the bar it
/// holds is the floor of this repository's testing and not the ceiling.
/// </remarks>
internal static class CoverageReport
{
    /// <summary>
    /// The environment variable the coverage workflow sets to the directory it
    /// collected into. Held here rather than in the workflow alone so the suite
    /// can name it when it refuses.
    /// </summary>
    internal const string ReportDirectoryVariable = "METADATASYNC_COVERAGE_REPORT";

    /// <summary>
    /// The name of the rendered report the run leaves beside the cobertura it
    /// was read from.
    /// </summary>
    internal const string RenderedReportName = "coverage-by-area.md";

    private const string TypePrefix = "Jellyfin.Plugin.MetadataSync.";

    /// <summary>
    /// The areas, and which of them is held to the bar that every branch is
    /// reached.
    ///
    /// The split follows #74: the decision code is reachable by tests without
    /// substituting anything, so a gap in it is a decision nothing exercises.
    /// The entry point and the service registration are not held to that bar,
    /// because covering them means asserting that a call was made and a test
    /// that asserts a call was made mostly tests itself.
    ///
    /// Configuration sits in the decision area for the validation that lives
    /// there. The accessor beside it is not decision code, and if it ever grows
    /// a line no test reaches it will redden this area rather than its own. That
    /// is the fail-closed direction and it is a known cost, not an oversight.
    /// </summary>
    private static readonly Area[] _areas = new[]
    {
        new Area(
            "Decision code",
            heldToTheBar: true,
            namespaces: new[] { "Conflicts", "Fields", "Matching", "References", "Configuration", "Reconciliation" },
            rootTypes: Array.Empty<string>()),
        new Area(
            "Entry point and registration",
            heldToTheBar: false,
            namespaces: Array.Empty<string>(),
            rootTypes: new[] { "Plugin", "PluginServiceRegistrator" }),
    };

    /// <summary>
    /// Reads a cobertura report.
    /// </summary>
    /// <param name="cobertura">The report text.</param>
    /// <returns>The reading, which carries its own refusals.</returns>
    internal static CoverageReading Read(string cobertura)
    {
        XElement root;
        try
        {
            root = XDocument.Parse(cobertura).Root ?? throw new XmlException("The report has no root element.");
        }
        catch (XmlException error)
        {
            return CoverageReading.RefusedBecause("The report is not readable as XML: " + error.Message);
        }

        var stated = root.Attribute("lines-valid");
        if (stated is null)
        {
            return CoverageReading.RefusedBecause(
                "The report states no lines-valid, which is what the collector writes when its filters matched no module. A run that measured nothing is not a clean result.");
        }

        if (!int.TryParse(stated.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var statedLines) || statedLines == 0)
        {
            return CoverageReading.RefusedBecause(
                "The report states lines-valid as '" + stated.Value + "', so nothing was measured.");
        }

        var classes = root.Descendants("class").ToList();
        if (classes.Count == 0)
        {
            return CoverageReading.RefusedBecause("The report names no class, so nothing was measured.");
        }

        var refusals = new List<string>();
        var totals = new Dictionary<string, Tally>(StringComparer.Ordinal);
        var unreached = new List<string>();

        foreach (var area in _areas)
        {
            totals[area.Name] = new Tally();
        }

        foreach (var measured in classes)
        {
            var name = measured.Attribute("name")?.Value ?? string.Empty;
            var area = AreaOf(name);
            if (area is null)
            {
                refusals.Add(
                    "No area claims '" + name
                    + "'. A namespace or a top-level type added to the plugin is assigned an area in CoverageReport, so a new part of the plugin is measured on purpose rather than folded into whichever area matched first.");
                continue;
            }

            var tally = totals[area.Name];

            // Named without the namespace every type here shares, because a
            // report where every row starts with the same twenty-nine characters
            // is one a reader scans past.
            var shortName = name[TypePrefix.Length..];
            foreach (var line in MethodLines(measured))
            {
                tally.Add(line);
                if (area.HeldToTheBar && !line.FullyReached)
                {
                    unreached.Add(shortName + " line " + line.Number.ToString(CultureInfo.InvariantCulture) + ", " + line.Shortfall);
                }
            }
        }

        var figures = _areas
            .Select(a => new AreaFigures(
                a.Name,
                a.HeldToTheBar,
                totals[a.Name].LinesCovered,
                totals[a.Name].LinesValid,
                totals[a.Name].BranchesCovered,
                totals[a.Name].BranchesValid))
            .ToList();

        AccountForEveryLineTheRunMeasured(root, statedLines, figures, refusals);

        return new CoverageReading(figures, unreached, refusals);
    }

    /// <summary>
    /// Renders a reading as the report the run keeps.
    /// </summary>
    /// <param name="reading">The reading.</param>
    /// <returns>The report text.</returns>
    internal static string Render(CoverageReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var text = new StringBuilder();
        text.AppendLine("# Coverage by area");
        text.AppendLine();
        text.AppendLine("Collected by the run that produced it. The areas are declared in");
        text.AppendLine("CoverageReport in the suite, and every class the run measured falls in one");
        text.AppendLine("of them. Only the areas marked as held are held to the bar that every line");
        text.AppendLine("and every branch is reached.");
        text.AppendLine();
        text.AppendLine("| Area | Held to the bar | Lines | Branches |");
        text.AppendLine("| --- | --- | --- | --- |");
        foreach (var area in reading.Areas)
        {
            text.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "| {0} | {1} | {2}/{3} | {4}/{5} |",
                area.Name,
                area.HeldToTheBar ? "yes" : "no",
                area.LinesCovered,
                area.LinesValid,
                area.BranchesCovered,
                area.BranchesValid));
        }

        text.AppendLine();
        if (reading.Unreached.Count == 0)
        {
            text.AppendLine("Every line and every branch in the areas held to the bar was reached.");
        }
        else
        {
            text.AppendLine("What was not reached in the areas held to the bar:");
            text.AppendLine();
            foreach (var gap in reading.Unreached)
            {
                text.AppendLine("- " + gap);
            }
        }

        if (reading.Refusals.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("This reading is refused:");
            text.AppendLine();
            foreach (var refusal in reading.Refusals)
            {
                text.AppendLine("- " + refusal);
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// The report says what it measured in its own root attributes, and the areas
    /// are a partition of the same classes. Where the two disagree, something was
    /// measured that no area claimed or counted twice, and neither is a number to
    /// publish.
    /// </summary>
    private static void AccountForEveryLineTheRunMeasured(
        XElement root,
        int statedLines,
        IReadOnlyCollection<AreaFigures> figures,
        List<string> refusals)
    {
        var summed = figures.Sum(f => f.LinesValid);
        if (summed != statedLines)
        {
            refusals.Add(string.Format(
                CultureInfo.InvariantCulture,
                "The report states {0} measurable line(s) and the areas account for {1}. The areas are meant to be a partition of what the run measured.",
                statedLines,
                summed));
        }

        var statedBranches = root.Attribute("branches-valid")?.Value;
        if (statedBranches is not null
            && int.TryParse(statedBranches, NumberStyles.Integer, CultureInfo.InvariantCulture, out var branches)
            && branches != figures.Sum(f => f.BranchesValid))
        {
            refusals.Add(string.Format(
                CultureInfo.InvariantCulture,
                "The report states {0} measurable branch(es) and the areas account for {1}.",
                branches,
                figures.Sum(f => f.BranchesValid)));
        }
    }

    private static Area? AreaOf(string typeName)
    {
        if (!typeName.StartsWith(TypePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var remainder = typeName[TypePrefix.Length..];

        foreach (var area in _areas)
        {
            foreach (var declared in area.Namespaces)
            {
                if (remainder.StartsWith(declared + ".", StringComparison.Ordinal))
                {
                    return area;
                }
            }

            foreach (var declared in area.RootTypes)
            {
                if (string.Equals(remainder, declared, StringComparison.Ordinal)
                    || remainder.StartsWith(declared + ".", StringComparison.Ordinal))
                {
                    return area;
                }
            }
        }

        return null;
    }

    private static IEnumerable<MeasuredLine> MethodLines(XElement measured)
    {
        return measured.Elements("methods")
            .Elements("method")
            .Elements("lines")
            .Elements("line")
            .Select(MeasuredLine.From);
    }

    private sealed class Area
    {
        internal Area(string name, bool heldToTheBar, IReadOnlyList<string> namespaces, IReadOnlyList<string> rootTypes)
        {
            Name = name;
            HeldToTheBar = heldToTheBar;
            Namespaces = namespaces;
            RootTypes = rootTypes;
        }

        internal string Name { get; }

        internal bool HeldToTheBar { get; }

        internal IReadOnlyList<string> Namespaces { get; }

        internal IReadOnlyList<string> RootTypes { get; }
    }

    private sealed class Tally
    {
        internal int LinesCovered { get; private set; }

        internal int LinesValid { get; private set; }

        internal int BranchesCovered { get; private set; }

        internal int BranchesValid { get; private set; }

        internal void Add(MeasuredLine line)
        {
            LinesValid++;
            if (line.Hits > 0)
            {
                LinesCovered++;
            }

            BranchesCovered += line.BranchesCovered;
            BranchesValid += line.BranchesValid;
        }
    }

    private sealed class MeasuredLine
    {
        private MeasuredLine(int number, int hits, int branchesCovered, int branchesValid)
        {
            Number = number;
            Hits = hits;
            BranchesCovered = branchesCovered;
            BranchesValid = branchesValid;
        }

        internal int Number { get; }

        internal int Hits { get; }

        internal int BranchesCovered { get; }

        internal int BranchesValid { get; }

        internal bool FullyReached => Hits > 0 && BranchesCovered == BranchesValid;

        /// <summary>
        /// Gets what is missing on this line, said in the terms the report used,
        /// so a reader can go to the line rather than to the collector.
        /// </summary>
        internal string Shortfall => Hits == 0
            ? "never executed"
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0} of {1} branch(es) reached",
                BranchesCovered,
                BranchesValid);

        internal static MeasuredLine From(XElement line)
        {
            var number = WholeNumber(line, "number");
            var hits = WholeNumber(line, "hits");
            var covered = 0;
            var valid = 0;

            // "50% (1/2)". Read as text rather than with an expression, because a
            // regular expression here would be a second language for one bracket.
            var condition = line.Attribute("condition-coverage")?.Value;
            if (condition is not null)
            {
                var open = condition.IndexOf('(', StringComparison.Ordinal);
                var slash = condition.IndexOf('/', StringComparison.Ordinal);
                var close = condition.IndexOf(')', StringComparison.Ordinal);
                if (open >= 0 && slash > open && close > slash)
                {
                    _ = int.TryParse(condition[(open + 1)..slash], NumberStyles.Integer, CultureInfo.InvariantCulture, out covered);
                    _ = int.TryParse(condition[(slash + 1)..close], NumberStyles.Integer, CultureInfo.InvariantCulture, out valid);
                }
            }

            return new MeasuredLine(number, hits, covered, valid);
        }

        private static int WholeNumber(XElement line, string attribute)
        {
            var value = line.Attribute(attribute)?.Value;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var read) ? read : 0;
        }
    }
}

/// <summary>
/// One area's figures.
/// </summary>
internal sealed class AreaFigures
{
    internal AreaFigures(
        string name,
        bool heldToTheBar,
        int linesCovered,
        int linesValid,
        int branchesCovered,
        int branchesValid)
    {
        Name = name;
        HeldToTheBar = heldToTheBar;
        LinesCovered = linesCovered;
        LinesValid = linesValid;
        BranchesCovered = branchesCovered;
        BranchesValid = branchesValid;
    }

    internal string Name { get; }

    internal bool HeldToTheBar { get; }

    internal int LinesCovered { get; }

    internal int LinesValid { get; }

    internal int BranchesCovered { get; }

    internal int BranchesValid { get; }
}

/// <summary>
/// One reading of one report.
/// </summary>
internal sealed class CoverageReading
{
    internal CoverageReading(
        IReadOnlyList<AreaFigures> areas,
        IReadOnlyList<string> unreached,
        IReadOnlyList<string> refusals)
    {
        Areas = areas;
        Unreached = unreached;
        Refusals = refusals;
    }

    /// <summary>Gets the figures, one row per declared area.</summary>
    internal IReadOnlyList<AreaFigures> Areas { get; }

    /// <summary>Gets every line in an area held to the bar that was not fully reached.</summary>
    internal IReadOnlyList<string> Unreached { get; }

    /// <summary>Gets why this reading may not be trusted, empty when it may.</summary>
    internal IReadOnlyList<string> Refusals { get; }

    internal static CoverageReading RefusedBecause(string reason) =>
        new(Array.Empty<AreaFigures>(), Array.Empty<string>(), new[] { reason });
}
