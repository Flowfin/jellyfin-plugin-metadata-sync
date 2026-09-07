using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the publish route to the agreement between the three numbers a
/// release carries: the tag it is cut from, the <c>version</c> the manifest
/// declares, and the version stamped into the assembly.
/// </summary>
/// <remarks>
/// The route already refuses each disagreement, in two steps of two jobs, and
/// until this file nothing held either of them in place. Both are shell inside
/// a workflow, so a build that compiles and a suite that passes say nothing
/// about them: deleting either step leaves every other check here green and
/// publishes a release whose catalogue entry, archive and assembly disagree
/// about which version an operator installed.
/// <para>
/// The join is the part worth reading twice. The gate compares the tag with
/// the manifest and hands the manifest's number forward as a job output; the
/// build holds the assembly to that same output rather than reading the
/// manifest a second time. Two steps each comparing a pair is not a three-way
/// agreement unless the value passed between them is the value that was
/// compared, so that is asserted rather than assumed.
/// </para>
/// <para>
/// What this cannot say is anything about a run. It reads a workflow file, so
/// what it judges is the shape of the refusal - an inequality between two
/// values with a non-zero exit inside it - and never whether a runner executed
/// one. It also identifies the two values compared by the text of the
/// assignments they come from, which is a weaker reading than the shape: a
/// step that fetched the manifest version by some route mentioning none of
/// those words would be read as comparing something else, and the repair is to
/// name the new route here rather than to loosen the reading.
/// </para>
/// </remarks>
public class ReleaseVersionAgreementTests
{
    /// <summary>
    /// The job that reads the tag and the manifest and refuses a disagreement
    /// between them.
    /// </summary>
    private const string GateJob = "gate";

    /// <summary>
    /// The job that compiles the plugin and refuses an assembly stamped with a
    /// version the gate did not approve.
    /// </summary>
    private const string BuildJob = "build";

    /// <summary>
    /// The step in <see cref="GateJob"/> that carries the first comparison.
    /// </summary>
    private const string TagStep = "Check the tag and build.yaml agree";

    /// <summary>
    /// The step in <see cref="BuildJob"/> that carries the second.
    /// </summary>
    private const string AssemblyStep = "Check the assembly version matches the manifest";

    /// <summary>
    /// The text an assignment carries when the value came out of the manifest.
    /// </summary>
    private const string ManifestRead = "read_scalar version";

    /// <summary>
    /// The text an assignment carries when the value came out of the tag the
    /// run was started from.
    /// </summary>
    private const string TagRead = "GITHUB_REF_NAME";

    /// <summary>
    /// The environment entry the build step holds the assembly against.
    /// </summary>
    private const string ApprovedVersion = "MANIFEST_VERSION";

    /// <summary>
    /// The text an assignment carries when the value was read out of the
    /// project rather than out of the manifest.
    /// </summary>
    private const string AssemblyRead = "AssemblyVersion";

    /// <summary>
    /// The expression the build step's environment entry must be, so the
    /// number the assembly is held to is the one the gate approved rather than
    /// a second reading of the manifest or a literal somebody typed.
    /// </summary>
    private const string GateOutput = "${{ needs.gate.outputs.version }}";

    /// <summary>
    /// A refusal, written the way the route writes one. It is the positive
    /// control for the four near-misses below: each of those differs from this
    /// in exactly one place, so a reading that passed all five would be
    /// reporting the step rather than the refusal.
    /// </summary>
    private const string Refuses = """
jobs:
  build:
    steps:
      - name: Check the assembly version matches the manifest
        env:
          MANIFEST_VERSION: ${{ needs.gate.outputs.version }}
        run: |
          assembly="$(dotnet msbuild -getProperty:AssemblyVersion -nologo)"
          want="${MANIFEST_VERSION}"
          got="${assembly}"
          if [ "${want}" != "${got}" ]; then
            echo "::error::The assembly is stamped ${assembly}."
            exit 1
          fi
""";

    /// <summary>
    /// The near-miss this file is written for. An annotation is not a refusal:
    /// a workflow command printing an error leaves the step's exit status at
    /// zero, so the run is red nowhere, the job succeeds and the release is
    /// published with the disagreement announced in a log. It is one deleted
    /// line away from the control above and it reads, in a diff, as the same
    /// check.
    /// </summary>
    private const string AnnouncesWithoutRefusing = """
jobs:
  build:
    steps:
      - name: Check the assembly version matches the manifest
        env:
          MANIFEST_VERSION: ${{ needs.gate.outputs.version }}
        run: |
          assembly="$(dotnet msbuild -getProperty:AssemblyVersion -nologo)"
          want="${MANIFEST_VERSION}"
          got="${assembly}"
          if [ "${want}" != "${got}" ]; then
            echo "::error::The assembly is stamped ${assembly}."
          fi
""";

    /// <summary>
    /// The same, with the exit present and zero. A status is what the runner
    /// reads, and zero is the one that publishes.
    /// </summary>
    private const string ExitsClean = """
jobs:
  build:
    steps:
      - name: Check the assembly version matches the manifest
        env:
          MANIFEST_VERSION: ${{ needs.gate.outputs.version }}
        run: |
          assembly="$(dotnet msbuild -getProperty:AssemblyVersion -nologo)"
          want="${MANIFEST_VERSION}"
          got="${assembly}"
          if [ "${want}" != "${got}" ]; then
            echo "::error::The assembly is stamped ${assembly}."
            exit 0
          fi
""";

    /// <summary>
    /// The same, with the exit commented out. A reading that stripped no shell
    /// comments would find the word and call this a refusal, which is the
    /// direction that fails open.
    /// </summary>
    private const string RefusalCommentedOut = """
jobs:
  build:
    steps:
      - name: Check the assembly version matches the manifest
        env:
          MANIFEST_VERSION: ${{ needs.gate.outputs.version }}
        run: |
          assembly="$(dotnet msbuild -getProperty:AssemblyVersion -nologo)"
          want="${MANIFEST_VERSION}"
          got="${assembly}"
          if [ "${want}" != "${got}" ]; then
            echo "::error::The assembly is stamped ${assembly}."
            # exit 1
          fi
""";

    /// <summary>
    /// The control again, with a comment that would reassign the compared
    /// variable to a literal if it were read. The refusal patterns are anchored
    /// to the start of a line and never match a comment; the assignment pattern
    /// is not, because an assignment may follow a semicolon or a case pattern,
    /// so it is the one reading a dropped comment filter would change. Without
    /// the filter the origin of <c>want</c> becomes the literal, the join is
    /// read as broken, and the route is refused for a comment.
    /// </summary>
    private const string OriginReassignedInAComment = """
jobs:
  build:
    steps:
      - name: Check the assembly version matches the manifest
        env:
          MANIFEST_VERSION: ${{ needs.gate.outputs.version }}
        run: |
          assembly="$(dotnet msbuild -getProperty:AssemblyVersion -nologo)"
          want="${MANIFEST_VERSION}"
          # want="0.1.1.0"
          got="${assembly}"
          if [ "${want}" != "${got}" ]; then
            echo "::error::The assembly is stamped ${assembly}."
            exit 1
          fi
""";

    /// <summary>
    /// The same, comparing for equality. It refuses every release whose
    /// versions agree and publishes every release whose versions do not, which
    /// is the check inverted rather than absent, and the first tag is where it
    /// would be found.
    /// </summary>
    private const string RefusesTheAgreement = """
jobs:
  build:
    steps:
      - name: Check the assembly version matches the manifest
        env:
          MANIFEST_VERSION: ${{ needs.gate.outputs.version }}
        run: |
          assembly="$(dotnet msbuild -getProperty:AssemblyVersion -nologo)"
          want="${MANIFEST_VERSION}"
          got="${assembly}"
          if [ "${want}" = "${got}" ]; then
            echo "::error::The assembly is stamped ${assembly}."
            exit 1
          fi
""";

    /// <summary>
    /// The same refusal, holding the assembly to a number written into the
    /// step instead of to the one the gate approved. Every leg about the
    /// comparison passes on it, and the release it publishes is the one this
    /// file exists against: the manifest and the assembly agree with each
    /// other and neither agrees with the tag.
    /// </summary>
    private const string HoldsItToItsOwnNumber = """
jobs:
  build:
    steps:
      - name: Check the assembly version matches the manifest
        env:
          MANIFEST_VERSION: "0.1.1.0"
        run: |
          assembly="$(dotnet msbuild -getProperty:AssemblyVersion -nologo)"
          want="${MANIFEST_VERSION}"
          got="${assembly}"
          if [ "${want}" != "${got}" ]; then
            echo "::error::The assembly is stamped ${assembly}."
            exit 1
          fi
""";

    /// <summary>
    /// The number of spaces a step's block scalar is indented by in this
    /// route. It is fixed rather than derived because the reading is of one
    /// file whose shape a reformatting would change visibly.
    /// </summary>
    private const int ScriptIndent = 10;

    /// <summary>
    /// An <c>if</c> whose condition is a string inequality between two
    /// expansions, which is the shape both refusals are written in.
    /// </summary>
    private static readonly Regex Inequality =
        new(@"^if\s+\[{1,2}\s+""\$\{(\w+)\}""\s*!=\s*""\$\{(\w+)\}""\s+\]{1,2}\s*;\s*then$");

    /// <summary>
    /// An exit that ends the job. Zero is excluded because it is the status
    /// the runner reads as success.
    /// </summary>
    private static readonly Regex NonZeroExit = new(@"^exit\s+[1-9][0-9]*$");

    /// <summary>
    /// An assignment to a shell variable. The name may be preceded only by the
    /// start of the line, whitespace, a semicolon or the closing parenthesis of
    /// a case pattern, so that an option written as <c>-p:Name=value</c> is not
    /// read as one.
    /// </summary>
    private static readonly Regex Assignment = new(@"(?:^|[\s;)])([A-Za-z_][A-Za-z0-9_]*)=");

    /// <summary>
    /// An expansion, with whatever operator it carries. The operator is
    /// dropped: what is asked of an origin is where the value came from, and
    /// <c>${tag%-stable}</c> comes from the same place as <c>${tag}</c>.
    /// </summary>
    private static readonly Regex Expansion = new(@"\$\{([A-Za-z_][A-Za-z0-9_]*)[^}]*\}");

    /// <summary>
    /// A line handing a value to the next job.
    /// </summary>
    private static readonly Regex JobOutput =
        new(@"^echo\s+""(\w+)=\$\{(\w+)\}""\s*>>\s*""\$GITHUB_OUTPUT""$");

    /// <summary>
    /// The reading reaches the route rather than an empty file, and both steps
    /// are where this file says they are. Without this every leg below would
    /// pass on a route that had lost the job it names.
    /// </summary>
    [Fact]
    public void TheReadingReachesBothStepsOfTheRoute()
    {
        Assert.NotEmpty(Steps(Route(), GateJob));
        Assert.NotEmpty(Steps(Route(), BuildJob));
        Assert.NotEmpty(StepNamed(Route(), GateJob, TagStep).Run);
        Assert.NotEmpty(StepNamed(Route(), BuildJob, AssemblyStep).Run);
    }

    /// <summary>
    /// The first comparison. A tag is the only way a release is cut here and
    /// its numeric part is the version an operator installs, so a tag naming a
    /// version the manifest does not declare publishes a package under a number
    /// nothing else in the tree carries.
    /// </summary>
    [Fact]
    public void TheTagIsRefusedWhenTheManifestDeclaresAnotherVersion()
    {
        var step = StepNamed(Route(), GateJob, TagStep);

        Assert.Single(Compared(step, ManifestRead, TagRead));
    }

    /// <summary>
    /// The second. The assembly version is what a server reads off the loaded
    /// plugin, and it is derived from the manifest by
    /// <c>Directory.Build.props</c> rather than typed, so what this refuses is
    /// the day somebody restates it in the project file and the two stop being
    /// one number.
    /// </summary>
    [Fact]
    public void TheAssemblyIsRefusedWhenItIsStampedWithAnotherVersion()
    {
        var step = StepNamed(Route(), BuildJob, AssemblyStep);

        Assert.Single(Compared(step, ApprovedVersion, AssemblyRead));
    }

    /// <summary>
    /// The join between the two, which is what makes three numbers agree
    /// rather than two pairs. The value the gate hands forward is the value it
    /// compared against the tag, and the value the build holds the assembly to
    /// is that output.
    /// </summary>
    [Fact]
    public void TheNumberTheAssemblyIsHeldToIsTheNumberTheTagWasHeldTo()
    {
        var route = Route();
        var gate = StepNamed(route, GateJob, TagStep);
        var build = StepNamed(route, BuildJob, AssemblyStep);

        var compared = Compared(gate, ManifestRead, TagRead).Single();
        var handed = Script(gate.Run)
            .Select(line => JobOutput.Match(line))
            .Where(m => m.Success)
            .ToList();

        var version = Assert.Single(
            handed,
            m => string.Equals(m.Groups[2].Value, compared.Left, StringComparison.Ordinal)
                || string.Equals(m.Groups[2].Value, compared.Right, StringComparison.Ordinal));


        Assert.Equal("${{ steps." + gate.Id + ".outputs." + version.Groups[1].Value + " }}", Map(route, GateJob, "outputs")[version.Groups[1].Value]);
        Assert.Equal(GateOutput, build.Env[ApprovedVersion]);
    }

    /// <summary>
    /// The control. The four legs below each delete or alter one thing in this
    /// fixture, so a reading that reported a refusal here and nowhere else is
    /// reading the refusal rather than the step around it.
    /// </summary>
    [Fact]
    public void ARefusalWrittenTheWayTheRouteWritesOneIsReadAsOne()
    {
        Assert.Single(Compared(StepNamed(Refuses, BuildJob, AssemblyStep), ApprovedVersion, AssemblyRead));
    }

    /// <summary>
    /// The bite this file is mostly for. An error annotation with no exit
    /// beside it is a step that succeeds, and the disagreement reaches the
    /// catalogue with a note about itself in a log nobody opens.
    /// </summary>
    [Fact]
    public void AnAnnotationWithNoExitBesideItIsNotARefusal()
    {
        Assert.Empty(Compared(StepNamed(AnnouncesWithoutRefusing, BuildJob, AssemblyStep), ApprovedVersion, AssemblyRead));
    }

    /// <summary>
    /// An exit the runner reads as success is not a refusal either.
    /// </summary>
    [Fact]
    public void AnExitOfZeroIsNotARefusal()
    {
        Assert.Empty(Compared(StepNamed(ExitsClean, BuildJob, AssemblyStep), ApprovedVersion, AssemblyRead));
    }

    /// <summary>
    /// A refusal that has been commented out is not one, and a reading that
    /// matched the word inside a comment would call it one.
    /// </summary>
    [Fact]
    public void ARefusalInsideACommentIsNotARefusal()
    {
        Assert.Empty(Compared(StepNamed(RefusalCommentedOut, BuildJob, AssemblyStep), ApprovedVersion, AssemblyRead));
    }

    /// <summary>
    /// The other direction of the comment filter. The leg above holds the
    /// filter to nothing, because every refusal pattern is anchored and a
    /// comment never starts with <c>exit</c>; the assignment pattern is not
    /// anchored, so a reading that kept comments would take an assignment out
    /// of one and refuse the route's join for it. This is the leg that goes
    /// red when the filter is dropped.
    /// </summary>
    [Fact]
    public void AnAssignmentInsideACommentIsNotAnOrigin()
    {
        Assert.Single(Compared(StepNamed(OriginReassignedInAComment, BuildJob, AssemblyStep), ApprovedVersion, AssemblyRead));
    }

    /// <summary>
    /// The comparison inverted. It refuses the releases that agree, which is
    /// the mistake a hurried edit makes, and no leg reading only for the
    /// presence of a comparison would separate it from the control.
    /// </summary>
    [Fact]
    public void AComparisonForEqualityIsNotARefusalOfADisagreement()
    {
        Assert.Empty(Compared(StepNamed(RefusesTheAgreement, BuildJob, AssemblyStep), ApprovedVersion, AssemblyRead));
    }

    /// <summary>
    /// The join broken while every other leg still passes. The step holds the
    /// assembly to a number of its own, so the manifest and the assembly agree
    /// with each other and the tag is held to nothing.
    /// </summary>
    [Fact]
    public void AStepHoldingTheAssemblyToItsOwnNumberIsNotTheJoin()
    {
        var step = StepNamed(HoldsItToItsOwnNumber, BuildJob, AssemblyStep);

        Assert.Single(Compared(step, ApprovedVersion, AssemblyRead));
        Assert.NotEqual(GateOutput, step.Env[ApprovedVersion]);
    }

    /// <summary>
    /// Reads the route the release is cut by, from the copy this project puts
    /// beside the test binary.
    /// </summary>
    /// <returns>The workflow file's text.</returns>
    private static string Route()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "publish.yaml"));

    /// <summary>
    /// The refusing inequalities in one step whose two sides came from the two
    /// named places.
    /// </summary>
    /// <param name="step">The step to read.</param>
    /// <param name="one">Text the assignment behind one side carries.</param>
    /// <param name="other">Text the assignment behind the other side carries.</param>
    /// <returns>The comparisons that match, which is expected to be one.</returns>
    private static IReadOnlyList<(string Left, string Right)> Compared(Step step, string one, string other)
    {
        var origins = Origins(step.Run);

        return RefusingInequalities(step.Run)
            .Where(pair => (Names(origins, pair.Left, one) && Names(origins, pair.Right, other))
                || (Names(origins, pair.Left, other) && Names(origins, pair.Right, one)))
            .ToList();
    }

    /// <summary>
    /// Whether the value a variable holds came from somewhere the given text
    /// names.
    /// </summary>
    /// <param name="origins">The assignments read out of the script.</param>
    /// <param name="variable">The variable's name.</param>
    /// <param name="text">The text an origin must carry.</param>
    /// <returns>True when it does.</returns>
    private static bool Names(IReadOnlyDictionary<string, string> origins, string variable, string text)
        => origins.TryGetValue(variable, out var origin) && origin.Contains(text, StringComparison.Ordinal);

    /// <summary>
    /// The one step of a job with the given name.
    /// </summary>
    /// <param name="workflow">The workflow file's text.</param>
    /// <param name="job">The job's key.</param>
    /// <param name="name">The step's name.</param>
    /// <returns>The step.</returns>
    private static Step StepNamed(string workflow, string job, string name)
        => Assert.Single(Steps(workflow, job), s => string.Equals(s.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// The steps a job declares, in the order the route runs them.
    /// </summary>
    /// <param name="workflow">The workflow file's text.</param>
    /// <param name="job">The job's key.</param>
    /// <returns>The steps.</returns>
    private static IReadOnlyList<Step> Steps(string workflow, string job)
    {
        var steps = new List<Step>();
        var inSteps = false;
        var inEnv = false;
        var inScript = false;

        var name = string.Empty;
        var id = string.Empty;
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        var script = new List<string>();

        void Close()
        {
            if (name.Length > 0 || id.Length > 0 || env.Count > 0 || script.Count > 0)
            {
                steps.Add(new Step(name, id, env, string.Join("\n", script)));
            }

            name = string.Empty;
            id = string.Empty;
            env = new Dictionary<string, string>(StringComparer.Ordinal);
            script = new List<string>();
            inEnv = false;
            inScript = false;
        }

        foreach (var line in JobLines(workflow, job))
        {
            var indent = line.Length - line.TrimStart(' ').Length;

            if (inScript && (line.Length == 0 || indent >= ScriptIndent))
            {
                script.Add(line.Length == 0 ? string.Empty : line[ScriptIndent..]);
                continue;
            }

            inScript = false;

            if (line.Length == 0)
            {
                continue;
            }

            var body = line.TrimStart(' ');

            if (indent == 4)
            {
                Close();
                inSteps = string.Equals(body, "steps:", StringComparison.Ordinal);
                continue;
            }

            if (!inSteps)
            {
                continue;
            }

            if (indent == 6 && body.StartsWith("- ", StringComparison.Ordinal))
            {
                Close();
                body = body[2..];
                indent = 8;
            }

            if (indent == 8)
            {
                inEnv = string.Equals(body, "env:", StringComparison.Ordinal);
                if (body.StartsWith("name:", StringComparison.Ordinal))
                {
                    name = Scalar(body[5..]);
                }
                else if (body.StartsWith("id:", StringComparison.Ordinal))
                {
                    id = Scalar(body[3..]);
                }
                else if (body.StartsWith("run:", StringComparison.Ordinal))
                {
                    inScript = true;
                    script.Clear();
                }

                continue;
            }

            if (indent == ScriptIndent && inEnv)
            {
                var colon = body.IndexOf(':', StringComparison.Ordinal);
                if (colon > 0)
                {
                    env[body[..colon]] = Scalar(body[(colon + 1)..]);
                }
            }
        }

        Close();
        return steps;
    }

    /// <summary>
    /// A map a job declares under one of its keys, such as its outputs.
    /// </summary>
    /// <param name="workflow">The workflow file's text.</param>
    /// <param name="job">The job's key.</param>
    /// <param name="key">The key the map is under.</param>
    /// <returns>The entries.</returns>
    private static IReadOnlyDictionary<string, string> Map(string workflow, string job, string key)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        var inMap = false;

        foreach (var line in JobLines(workflow, job))
        {
            if (line.Length == 0)
            {
                continue;
            }

            var indent = line.Length - line.TrimStart(' ').Length;
            var body = line.TrimStart(' ');

            if (indent == 4)
            {
                inMap = string.Equals(body, key + ":", StringComparison.Ordinal);
                continue;
            }

            if (indent == 6 && inMap && !body.StartsWith('#'))
            {
                var colon = body.IndexOf(':', StringComparison.Ordinal);
                if (colon > 0)
                {
                    entries[body[..colon]] = Scalar(body[(colon + 1)..]);
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// The lines one job declares, indentation kept, so the readings above are
    /// one walk of the file rather than each of them finding the job again.
    /// </summary>
    /// <param name="workflow">The workflow file's text.</param>
    /// <param name="job">The job's key.</param>
    /// <returns>The job's own lines, in the order they are written.</returns>
    private static IReadOnlyList<string> JobLines(string workflow, string job)
    {
        var lines = new List<string>();
        var inJobs = false;
        var inJob = false;

        foreach (var line in workflow.Split('\n').Select(raw => raw.TrimEnd('\r').TrimEnd()))
        {
            var indent = line.Length - line.TrimStart(' ').Length;

            if (line.Length == 0)
            {
                if (inJob)
                {
                    lines.Add(string.Empty);
                }

                continue;
            }

            if (indent == 0)
            {
                inJobs = string.Equals(line, "jobs:", StringComparison.Ordinal);
                inJob = false;
                continue;
            }

            if (!inJobs)
            {
                continue;
            }

            if (indent == 2)
            {
                inJob = string.Equals(line.TrimStart(' '), job + ":", StringComparison.Ordinal);
                continue;
            }

            if (inJob)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    /// <summary>
    /// A scalar as it is written in the route, without the quotes a value may
    /// carry.
    /// </summary>
    /// <param name="text">The text after the key.</param>
    /// <returns>The value.</returns>
    private static string Scalar(string text)
    {
        var value = text.Trim();
        if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
        {
            value = value[1..^1];
        }

        return value;
    }

    /// <summary>
    /// A step's script as lines a reading can judge: comments removed,
    /// continuations joined, and nothing else changed.
    /// </summary>
    /// <param name="run">The script.</param>
    /// <returns>The lines.</returns>
    private static IReadOnlyList<string> Script(string run)
    {
        var joined = new List<string>();
        var pending = string.Empty;

        // The projection and the filter sit in the sequence rather than at the
        // top of the body, so the loop holds only the join. A body that begins
        // with one test and a `continue` is the shape the analysis reads as a
        // missed `Where`, and the two rewrites are one change rather than one
        // and then its correction a scan later.
        foreach (var line in run.Split('\n')
            .Select(raw => raw.TrimEnd('\r').Trim())
            .Where(text => text.Length > 0 && !text.StartsWith('#')))
        {
            if (line.EndsWith('\\'))
            {
                pending += line[..^1].TrimEnd() + " ";
                continue;
            }

            joined.Add(pending + line);
            pending = string.Empty;
        }

        if (pending.Length > 0)
        {
            joined.Add(pending.TrimEnd());
        }

        return joined;
    }

    /// <summary>
    /// The inequalities in a script that end the job when they hold. The exit
    /// is required to be in the branch the condition guards rather than
    /// anywhere after it, because a step exiting for some later reason is not
    /// this comparison refusing.
    /// </summary>
    /// <param name="run">The script.</param>
    /// <returns>The variable names each refusing comparison holds against each other.</returns>
    private static IReadOnlyList<(string Left, string Right)> RefusingInequalities(string run)
    {
        var found = new List<(string Left, string Right)>();
        var lines = Script(run);

        for (var i = 0; i < lines.Count; i++)
        {
            var opener = Inequality.Match(lines[i]);
            if (!opener.Success)
            {
                continue;
            }

            var depth = 1;
            var refuses = false;
            for (var j = i + 1; j < lines.Count && depth > 0; j++)
            {
                if (lines[j].StartsWith("if ", StringComparison.Ordinal))
                {
                    depth++;
                }
                else if (string.Equals(lines[j], "fi", StringComparison.Ordinal))
                {
                    depth--;
                }
                else if (depth == 1 && NonZeroExit.IsMatch(lines[j]))
                {
                    refuses = true;
                }
            }

            if (refuses)
            {
                found.Add((opener.Groups[1].Value, opener.Groups[2].Value));
            }
        }

        return found;
    }

    /// <summary>
    /// Where each variable in a script got its value, with the expansions it
    /// was assigned from resolved as far as the script itself declares them.
    /// </summary>
    /// <param name="run">The script.</param>
    /// <returns>Each variable against the text of its last assignment.</returns>
    private static IReadOnlyDictionary<string, string> Origins(string run)
    {
        var origins = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in Script(run))
        {
            var assignment = Assignment.Match(line);
            if (!assignment.Success)
            {
                continue;
            }

            origins[assignment.Groups[1].Value] = Resolve(line[(assignment.Index + assignment.Length)..], origins);
        }

        return origins;
    }

    /// <summary>
    /// One assignment's right-hand side with the expansions in it replaced by
    /// what those variables were last assigned.
    /// </summary>
    /// <param name="text">The right-hand side.</param>
    /// <param name="origins">The assignments seen so far.</param>
    /// <returns>The resolved text.</returns>
    private static string Resolve(string text, IReadOnlyDictionary<string, string> origins)
    {
        var resolved = text;

        for (var pass = 0; pass < 4; pass++)
        {
            var next = Expansion.Replace(
                resolved,
                match => origins.TryGetValue(match.Groups[1].Value, out var origin) ? origin : match.Value);

            if (string.Equals(next, resolved, StringComparison.Ordinal))
            {
                break;
            }

            resolved = next;
        }

        return resolved.Trim();
    }

    /// <summary>
    /// One step of the route, in the parts a version agreement is read out of.
    /// </summary>
    /// <param name="Name">The step's name.</param>
    /// <param name="Id">The step's id, which is how a later job names its outputs.</param>
    /// <param name="Env">The environment the step runs with.</param>
    /// <param name="Run">The script the step runs.</param>
    private sealed record Step(string Name, string Id, IReadOnlyDictionary<string, string> Env, string Run);
}
