using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the publish route to the one property an operator cannot check for
/// themselves: that a release carrying fewer assets than the route promises
/// cannot be published at all.
///
/// The route produces the archive in one job, the package inventory in a
/// second and the provenance attestation in a third, and the job that creates
/// the release waits on all three. That is what makes a failed inventory or a
/// failed attestation leave the release unreachable rather than publishing
/// what did succeed. It is one line in a workflow file, it reads as
/// scheduling, and deleting it produces a route that still passes every other
/// check here and publishes a release missing an asset.
///
/// What this cannot say is anything about a run. Nothing here has ever been
/// tagged, so the route has never executed, and a dependency between two jobs
/// is a promise this file reads out of the workflow rather than a behaviour
/// anybody has watched. It is the structure that is guarded, and the first run
/// is still the first run.
/// </summary>
public class ReleaseRouteTests
{
    /// <summary>
    /// The job that creates the release and attaches the assets.
    /// </summary>
    private const string ReleaseJob = "release";

    /// <summary>
    /// Every job that produces something a release carries. Each one is a
    /// separate job because each needs a different permission, and that is the
    /// same reason none of them can be folded into the job that publishes.
    /// </summary>
    private static readonly string[] AssetJobs = ["build", "attest", "inventory"];

    /// <summary>
    /// The route as a fixture, in the shape a reformatting would produce: the
    /// same dependency written as a block list rather than inline. A reader
    /// that only understood one of the two spellings would pass over the other
    /// having found no dependency at all, which is the direction that fails
    /// open.
    /// </summary>
    private const string BlockListForm = """
jobs:
  build:
    name: Build package
  attest:
    needs: build
  inventory:
    name: Write the package inventory
  release:
    needs:
      - build
      - attest
      - inventory
""";

    /// <summary>
    /// A route that publishes without waiting for two of the three. It is the
    /// mistake this file exists for, written down, so the reading is proved to
    /// report an absence rather than proved only against a file that has none.
    /// </summary>
    private const string WaitsOnLess = """
jobs:
  build:
    name: Build package
  attest:
    needs: build
  inventory:
    name: Write the package inventory
  release:
    needs: [build]
""";

    /// <summary>
    /// The reading is done against the file the route is, copied to the output
    /// by this project. Reading it by walking up from the test binary would
    /// answer a different question on a machine where the suite runs from
    /// somewhere else.
    /// </summary>
    [Fact]
    public void TheRouteReachesTheSweep()
    {
        var jobs = Jobs(Route());

        Assert.NotEmpty(jobs);
        Assert.Contains(ReleaseJob, jobs.Keys);
    }

    /// <summary>
    /// The property itself. A release cannot be published with an asset job
    /// missing, because the job that publishes waits on each of them.
    /// </summary>
    [Fact]
    public void TheReleaseWaitsOnEveryJobThatProducesAnAsset()
    {
        var waitsOn = Jobs(Route())[ReleaseJob];

        Assert.Equal(AssetJobs.OrderBy(j => j, StringComparer.Ordinal), waitsOn.OrderBy(j => j, StringComparer.Ordinal));
    }

    /// <summary>
    /// A dependency naming a job the route does not declare is refused by the
    /// runner at read time and by nothing here, so it would arrive as a route
    /// that fails on the one run nobody can repeat. Every name the release
    /// waits on is a job in the same file.
    /// </summary>
    [Fact]
    public void EveryJobTheReleaseWaitsOnIsDeclaredInTheRoute()
    {
        var jobs = Jobs(Route());

        Assert.All(jobs[ReleaseJob], name => Assert.Contains(name, jobs.Keys));
    }

    /// <summary>
    /// The attestation is about the archive this run built, so the job that
    /// mints it waits on the job that produced one. An attestation job running
    /// beside the build rather than after it would sign whatever it found,
    /// which is the failure that makes an attestation worth less than nothing.
    /// </summary>
    [Fact]
    public void TheAttestationIsMadeAfterTheArchiveExists()
    {
        var jobs = Jobs(Route());

        Assert.Contains("build", jobs["attest"]);
    }

    /// <summary>
    /// The bite, on a route that publishes without waiting for the inventory or
    /// the attestation. Without this leg the reading above is proved only
    /// against a file that has nothing wrong with it.
    /// </summary>
    [Fact]
    public void ARouteThatPublishesWithoutTheOtherAssetsIsReadAsOne()
    {
        var waitsOn = Jobs(WaitsOnLess)[ReleaseJob];

        Assert.Equal(["build"], waitsOn);
    }

    /// <summary>
    /// The same dependency written the other legal way round. A reader that
    /// stopped understanding this spelling would report no dependency at all
    /// and every leg above it would still be green.
    /// </summary>
    [Fact]
    public void ADependencyWrittenAsABlockListIsRead()
    {
        var waitsOn = Jobs(BlockListForm)[ReleaseJob];

        Assert.Equal(AssetJobs.OrderBy(j => j, StringComparer.Ordinal), waitsOn.OrderBy(j => j, StringComparer.Ordinal));
    }

    private static string Route()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "publish.yaml"));

    /// <summary>
    /// Every job the route declares, with the jobs each one waits on. It reads
    /// the two spellings a dependency is written in and nothing else, because
    /// what it is asked is which jobs a job waits on rather than what a
    /// workflow means.
    /// </summary>
    /// <param name="workflow">The workflow file's text.</param>
    /// <returns>Each declared job, against the jobs it waits on.</returns>
    private static Dictionary<string, List<string>> Jobs(string workflow)
    {
        var jobs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var inJobs = false;
        var job = string.Empty;
        var collecting = false;

        foreach (var raw in workflow.Split('\n'))
        {
            var line = raw.TrimEnd('\r').TrimEnd();
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            if (!line.StartsWith(' '))
            {
                inJobs = string.Equals(line, "jobs:", StringComparison.Ordinal);
                job = string.Empty;
                collecting = false;
                continue;
            }

            if (!inJobs)
            {
                continue;
            }

            var indent = line.Length - line.TrimStart(' ').Length;
            var body = line.TrimStart(' ');

            if (indent == 2 && body.EndsWith(':'))
            {
                job = body[..^1];
                jobs[job] = [];
                collecting = false;
                continue;
            }

            if (job.Length == 0)
            {
                continue;
            }

            if (collecting)
            {
                if (indent == 6 && body.StartsWith("- ", StringComparison.Ordinal))
                {
                    jobs[job].Add(body[2..].Trim());
                    continue;
                }

                collecting = false;
            }

            if (indent != 4 || !body.StartsWith("needs:", StringComparison.Ordinal))
            {
                continue;
            }

            var declared = body["needs:".Length..].Trim();
            if (declared.Length == 0)
            {
                collecting = true;
                continue;
            }

            jobs[job].AddRange(
                declared.Trim('[', ']')
                    .Split(',')
                    .Select(name => name.Trim())
                    .Where(name => name.Length > 0));
        }

        return jobs;
    }
}
