using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the list <c>docs/testing.md</c> gives of what holds its own policy up,
/// and the one claim in that list a sentence cannot check for itself.
///
/// That page carried two answers to one question. Under
/// <c>## What holds the policy up</c> it said two checks were the whole of the
/// enforcement and that all four of the policy's refusals were prose; fifty
/// lines further down, under <c>## Where the suite runs</c>, it described a
/// container run with no network interface and said that is where the network
/// half becomes a fact. The run landed on 2026-08-08 and the sentence denying it
/// was rewritten on 2026-08-23, so the file spent fifteen days telling a reader
/// whichever answer they stopped at.
///
/// What is read here, and in which direction, because the two halves of the list
/// are not equally derivable.
///
/// The routes are read as a fenced list rather than out of the prose. Each entry
/// naming a type is resolved against this assembly and each entry naming a path
/// against the file the project copied, so a name that stops existing reddens the
/// page. That is one direction. Which routes hold a policy up is a judgement
/// about what a check reaches, and no reading of this tree makes it, so a fourth
/// route added tomorrow and left out of the list is not refused here and the page
/// says so rather than leaving a reader to assume the list is complete.
///
/// The workflow half is read in both directions and is the half that can go
/// wrong without anybody editing the page. The job name the document quotes is
/// the name that file declares, so a rename on either side is red. And every run
/// of the suite that workflow starts carries the flag the claim rests on, so a
/// second test step added without it is red too - which is the direction a list
/// of one spelling would miss, because it would go on finding the first.
///
/// What it cannot reach, stated rather than left to be assumed. It judges the
/// file that declares the run and never a run: whether the container really had
/// no interface is what the job prints at the end of its own output, and nothing
/// here reads that. It does not judge whether the flag does what the page says it
/// does. And the four refusals themselves are still prose - nothing reads a test
/// body and decides whether it opened a window - which is the residual the page
/// keeps stating and this does not narrow.
/// </summary>
public class TestingStatementTests
{
    /// <summary>
    /// The comment that opens the fence around the routes the page names.
    /// </summary>
    private const string RoutesOpen = "<!-- what holds any part of this policy up: one per line, the name first, read by TestingStatementTests -->";

    /// <summary>
    /// The comment that closes it.
    /// </summary>
    private const string RoutesClose = "<!-- end of what holds this policy up -->";

    /// <summary>
    /// The marker a fenced line opens with, up to and including the backtick the
    /// name starts at.
    /// </summary>
    private const string EntryOpen = "- `";

    /// <summary>
    /// The job the page credits with making the network half a fact, spelled as
    /// the page spells it and as the workflow declares it. It is also the name
    /// the check appears under on a pull request.
    /// </summary>
    private const string Job = "Suite with no display and no network";

    /// <summary>
    /// The flag the whole claim rests on.
    /// </summary>
    private const string NoNetwork = "--network none";

    /// <summary>
    /// The command a container is started with. Every run of the suite is one of
    /// these, and the reading below is per run rather than per file, because a
    /// file carrying the flag once says nothing about a second run beside it.
    /// </summary>
    private const string Run = "docker run";

    /// <summary>
    /// The command under test, as the workflow spells it.
    /// </summary>
    private const string Suite = "dotnet test";

    /// <summary>
    /// The document, copied to the output for the reason every other document
    /// read here is: walking up from the test binary answers a different
    /// question on a machine where the tests run from somewhere else.
    /// </summary>
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "testing.md");

    /// <summary>
    /// The workflow the document credits, copied beside it.
    /// </summary>
    private static readonly string _workflow = Path.Combine(AppContext.BaseDirectory, "headless.yml");

    /// <summary>
    /// Every route the page names is in this tree. One direction, for the reason
    /// the summary gives.
    /// </summary>
    [Fact]
    public void EveryRouteThePageNamesIsThere()
    {
        var routes = Routes();

        // A fence that stopped parsing would pass every assertion below it
        // having read nothing, which is the one way this leg fails open.
        Assert.NotEmpty(routes);

        foreach (var route in routes)
        {
            Assert.True(Exists(route), route + " is named as holding this policy up and is nowhere in this tree.");
        }
    }

    /// <summary>
    /// The run the page credits with the network half is among the routes it
    /// lists. Without this the fence could name the two checks alone and every
    /// other leg here would still pass, which is the state the page was in.
    /// </summary>
    [Fact]
    public void TheRunThatHoldsTheNetworkHalfIsOneOfThem()
    {
        Assert.Contains(Routes(), route => route.EndsWith("headless.yml", StringComparison.Ordinal));
    }

    /// <summary>
    /// The job the page quotes is the job that file declares. Both directions: a
    /// rename in the workflow leaves the page quoting a job nobody runs, and a
    /// rename on the page leaves a reader looking for a check that is not there.
    /// </summary>
    [Fact]
    public void TheJobThePageQuotesIsTheJobTheWorkflowDeclares()
    {
        Assert.Contains(Job, File.ReadAllText(_document), StringComparison.Ordinal);
        Assert.Contains("name: " + Job, File.ReadAllText(_workflow), StringComparison.Ordinal);
    }

    /// <summary>
    /// Every run of the suite that workflow starts has no network interface. The
    /// reading is per container rather than per file, so a second test run added
    /// beside the first without the flag is refused rather than covered by it.
    /// </summary>
    [Fact]
    public void EveryRunOfTheSuiteInThatWorkflowHasNoNetwork()
    {
        var runs = SuiteRuns();

        // A workflow that has stopped running the suite at all is the direction
        // this would otherwise pass in, having found no run to judge.
        Assert.NotEmpty(runs);

        foreach (var run in runs)
        {
            Assert.Contains(NoNetwork, run, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The names between the backticks of the fenced list.
    /// </summary>
    private static IReadOnlyList<string> Routes()
    {
        var lines = File.ReadAllLines(_document);
        var open = Array.FindIndex(lines, line => line.Trim().Equals(RoutesOpen, StringComparison.Ordinal));
        var close = Array.FindIndex(lines, line => line.Trim().Equals(RoutesClose, StringComparison.Ordinal));

        Assert.True(open >= 0, "The fence naming what holds this policy up is not in " + _document + ".");
        Assert.True(close > open, "The fence naming what holds this policy up is not closed in " + _document + ".");

        var routes = new List<string>();
        for (var i = open + 1; i < close; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith(EntryOpen, StringComparison.Ordinal))
            {
                continue;
            }

            var rest = line[EntryOpen.Length..];
            var end = rest.IndexOf('`', StringComparison.Ordinal);
            Assert.True(end > 0, "A fenced entry opens a name and does not close it: " + line);
            routes.Add(rest[..end]);
        }

        return routes;
    }

    /// <summary>
    /// Whether a named route is in this tree. A name carrying a path separator is
    /// a file the project copied beside this binary; anything else is a type this
    /// assembly declares.
    ///
    /// This reads a path out of a document like the three legs behind
    /// <see cref="DeclaredPath"/>, and it deliberately does not go through that
    /// helper. <c>Path.GetFileName</c> cannot return a rooted path, so the
    /// discard those three were fixed for cannot happen here. What it would cost
    /// is a change of subject rather than a tightening: this leg resolves the
    /// file NAME beside the binary, because the project copies those documents
    /// there flat. A route written as <c>docs/testing.md</c> resolves today and
    /// would answer no through a helper that keeps the directory the document
    /// wrote, so moving it is a different leg rather than a safer one.
    /// </summary>
    private static bool Exists(string route)
    {
        if (route.Contains('/', StringComparison.Ordinal))
        {
            return File.Exists(Path.Combine(AppContext.BaseDirectory, Path.GetFileName(route)));
        }

        return typeof(TestingStatementTests).Assembly
            .GetTypes()
            .Any(type => string.Equals(type.Name, route, StringComparison.Ordinal));
    }

    /// <summary>
    /// The container invocations in the workflow that run the suite, each as the
    /// text from its own <c>docker run</c> up to the next one.
    ///
    /// Comment lines are dropped first. The header of that file discusses the
    /// flag and the command in prose, and a reading that counted those would find
    /// the claim satisfied by the sentence describing it.
    /// </summary>
    private static IReadOnlyList<string> SuiteRuns()
    {
        var body = File.ReadAllLines(_workflow)
            .Where(line => !line.TrimStart().StartsWith('#'))
            .ToList();

        var runs = new List<string>();
        var current = new List<string>();
        var started = false;

        foreach (var line in body)
        {
            if (line.Contains(Run, StringComparison.Ordinal))
            {
                if (started)
                {
                    runs.Add(string.Join('\n', current));
                }

                current = new List<string>();
                started = true;
            }

            if (started)
            {
                current.Add(line);
            }
        }

        if (started)
        {
            runs.Add(string.Join('\n', current));
        }

        return runs.Where(run => run.Contains(Suite, StringComparison.Ordinal)).ToList();
    }
}
