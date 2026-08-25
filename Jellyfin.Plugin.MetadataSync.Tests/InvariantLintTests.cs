using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The invariant lint. Several rules in this plan are a spelling rather than a
/// judgement, and a spelling is a thing a token scan decides in a second
/// instead of a thing review has to notice every time.
///
/// It is a test rather than a separate tool for two reasons. The suite is the
/// route that already produces a required check, so a rule added here runs on
/// every pull request without anything being switched on. And a token scan over
/// source text is what the suite already does twice, in
/// <see cref="StaticInstanceTests"/> and <see cref="DefaultLevelLoggingTests"/>,
/// so this adds a rule table to a shape the tree carries rather than a second
/// language and a second place to look.
///
/// Every rule below carries three things beside its pattern: the invariant it
/// enforces, the issue that declared it, and what it cannot catch. The third is
/// the one that is easy to leave out and the one that decides whether a reader
/// can trust a green run. A token pattern matches a spelling and never an
/// intent, so a rule that says nothing about its own reach is a claim wearing a
/// guard's clothes.
///
/// Two bounds hold for the whole table and are stated once here rather than
/// repeated in every record. The scan is a line scan and not a parse, so a
/// token inside a string literal or a block comment counts and a construction
/// split across two lines does not. And the scope of every rule is every plugin
/// source file, which is wider than the invariants as declared: three of them
/// name a path that does not exist yet. Wider is the fail-closed direction. A
/// legitimate use outside the narrow path costs one entry in that rule's
/// allowed set, with the reason written next to it, and a use inside the narrow
/// path costs a red suite.
/// </summary>
public class InvariantLintTests
{
    /// <summary>
    /// How a rule decides that a set of files is in breach.
    /// </summary>
    private enum Refusal
    {
        /// <summary>Any occurrence of any token is a finding.</summary>
        AnyOccurrence,

        /// <summary>The first occurrence is the declaration; every later one is a finding.</summary>
        EveryOccurrenceAfterTheFirst,
    }

    /// <summary>
    /// The seeded rules. Adding an invariant to this plan without adding a row
    /// here is refused by
    /// <see cref="TheRuleTableMatchesTheInvariantsTheGuideDeclares"/> rather
    /// than left to a reader of the guide.
    /// </summary>
    private static readonly Rule[] Rules = new[]
    {
        new Rule(
            Id: "no-file-system-property-in-item-identity",
            GuidePhrase: "no file-system property in the resolution path",
            Invariant: "No path, filename, directory name, size or hash of a file takes part in deciding that two items are the same.",
            DeclaredBy: "#28",
            Kind: Refusal.AnyOccurrence,
            TokenPatterns: new[] { "System.IO", "Path.", "File.", "FileInfo", "DirectoryInfo", "Directory." },
            AllowedIn: new[]
            {
                ("WrittenValues.cs",
                 "Is the plugin's own store, which keeps what this plugin wrote in a file beside the plugin's data, so it names the file-system types on purpose. The invariant is about deciding that two items are the same, and nothing here takes part in that: the item arrives as an identifier the resolver already produced, it is written down and read back, and no property of a file, a path or a directory reaches a comparison. What the allowance costs is stated rather than hidden - a file-system read added to this file for some other reason is not refused by this rule, and what stands against that is the review and the walk in ResolutionPathTests, which starts at the resolvers and would reach a store only if a resolution called into one."),
            },
            Regression: "var identity = Path.GetFileNameWithoutExtension(local.Path);",
            RegressionIsTheMistakeThat: "reaches for the filename when the provider identifiers came back empty, which is what every prior attempt in this space does.",
            NearMiss: "var identity = PathPolicyNeverReadsTheFileSystem(local);",
            NearMissIsTheNeighbourThat: "differs from the token by the one character after it, so a pattern matching the bare word would refuse a legitimate line.",
            CannotCatch: "a file-system property read somewhere else and handed in as a plain string, and any route to one that spells none of these tokens, including reflection and an extension method named after the thing it returns."),

        new Rule(
            Id: "no-transport-type-in-the-planner",
            GuidePhrase: "no transport type reachable from a reconciliation path",
            Invariant: "The half of a pass that decides what should change depends on no transport, so every rule in this plan is testable from a table with nothing running.",
            DeclaredBy: "#35",
            Kind: Refusal.AnyOccurrence,
            TokenPatterns: new[] { "System.Net", "HttpClient", "HttpRequestMessage", "WebClient", "Socket" },
            Regression: "using System.Net.Http;",
            RegressionIsTheMistakeThat: "puts a transport where the decision is made, which is what makes a reconciler need a network to test.",
            NearMiss: "var reply = await contract.SendAsync(payload).ConfigureAwait(false);",
            NearMissIsTheNeighbourThat: "is the legitimate way the same work is reached, through the contract. A transport type has no near spelling, so this neighbour is a legitimate line rather than a one-character one, and that is a weaker proof than the other four.",
            CannotCatch: "reachability, which is the word the invariant uses. A transport behind an injected interface spells none of these tokens at the site that uses it, so this refuses the direct naming and never the graph."),

        new Rule(
            Id: "no-static-instance-outside-the-entry-point",
            GuidePhrase: "no static instance outside the entry point",
            Invariant: "The static plugin instance is read by the entry point and the registration point and by nothing else, because a service that reaches it cannot be given a different configuration in a test.",
            DeclaredBy: "#8",
            RefusedBy: nameof(StaticInstanceTests),
            CannotCatch: "the same bounds that test states for itself. This row exists so the invariant is in the table with the other four, and it deliberately carries no pattern: a second scan for the same spelling would be a copy that drifts against the one already in the suite."),

        new Rule(
            Id: "one-contract-version-literal",
            GuidePhrase: "no second contract version literal",
            Invariant: "One constant declares the contract version this plugin was built against, and nothing else restates the number.",
            DeclaredBy: "#21",
            Kind: Refusal.EveryOccurrenceAfterTheFirst,
            TokenPatterns: new[] { "ContractVersion" },
            LiteralOnly: true,
            Regression: "public const string ContractVersion = \"1.2\";\nprivate const string FallbackContractVersion = \"1.1\";",
            RegressionIsTheMistakeThat: "adds a second number for a version this plugin will work around, which is the shape #21 refuses at registration.",
            NearMiss: "public const string ContractVersion = \"1.2\";\nprivate static bool Supports(string offered) => string.Equals(offered, ContractVersion, StringComparison.Ordinal);",
            NearMissIsTheNeighbourThat: "names the constant on the second line instead of restating the literal, which is the one-word difference between the mistake and the correct line.",
            CannotCatch: "a version number written on a line that does not name the constant at all, and the whole question of whether the one declared number is the right one."),

        new Rule(
            Id: "one-purpose-literal",
            GuidePhrase: "no second purpose literal",
            Invariant: "One constant declares the purpose this plugin registers for, and nothing else restates the string, because a purpose is the key a payload is routed by and a second spelling of it is a second consumer.",
            DeclaredBy: "#24",
            Kind: Refusal.EveryOccurrenceAfterTheFirst,
            TokenPatterns: new[] { "Purpose" },
            LiteralOnly: true,
            Regression: "public const string Purpose = \"flowfin.metadata-sync\";\nprivate const string LegacyPurpose = \"flowfin.metadatasync\";",
            RegressionIsTheMistakeThat: "keeps a second purpose for a peer that registered under an older spelling, so this plugin answers under two routing keys and the separation the contract asserts between two sync plugins on one server stops holding.",
            NearMiss: "public const string Purpose = \"flowfin.metadata-sync\";\nprivate static bool RegisteredFor(string offered) => string.Equals(offered, Purpose, StringComparison.Ordinal);",
            NearMissIsTheNeighbourThat: "names the constant on the second line instead of restating the string, which is the one-word difference between the mistake and the correct line.",
            CannotCatch: "a purpose written on a line naming no constant, and whether the one string declared is the one the peer registers for. It says nothing about registration either: the plugin registers for nothing today, and what a registration asks for is a call rather than a spelling."),

        new Rule(
            Id: "no-timestamp-from-one-server-against-the-other",
            GuidePhrase: "no timestamp from one server compared against the other's",
            Invariant: "No rule compares a timestamp from one server against a timestamp from the other, because nothing establishes that the two clocks are comparable.",
            DeclaredBy: "#46",
            Kind: Refusal.AnyOccurrence,
            TokenPatterns: new[] { "DateLastSaved", "DateModified", "DateTime.UtcNow", "DateTime.Now", "DateTimeOffset.UtcNow", "DateTimeOffset.Now" },
            AllowedIn: new[]
            {
                ("LibraryPlanTarget.cs",
                 "Reads this server's last-saved stamp on an item it fetched, and compares it against the same server's earlier reading of the same item, which is #41. That is one clock held against itself. The rule is about a stamp from one server held against a stamp from the other, and this file cannot make that comparison: the peer's stamp is on no type it can reach. The stamp leaves the file as a string, so nothing downstream can order two of them."),
            },
            Regression: "if (local.DateLastSaved > peer.DateLastSaved)",
            RegressionIsTheMistakeThat: "is the obvious conflict rule, and it hands every field to whichever server's clock is ahead, permanently and silently.",
            NearMiss: "if (local.LastValueWrittenByThisPlugin is not null)",
            NearMissIsTheNeighbourThat: "answers the same question causally instead of temporally, which is the mechanism #16 provides in place of a clock.",
            CannotCatch: "the comparison itself. It refuses the ingredients rather than the dish, so two timestamps obtained under other names and compared would pass, and an injected clock read through an interface spells none of these tokens."),

        new Rule(
            Id: "no-direction-comparison-outside-the-direction-type",
            GuidePhrase: "no direction comparison outside the direction type",
            Invariant: "One type holds which way metadata moves for a pairing, and nothing else names a member of it, because a direction read in four places disagrees with itself in one of them.",
            DeclaredBy: "#34",
            Kind: Refusal.AnyOccurrence,
            TokenPatterns: new[] { "SyncDirection." },
            AllowedIn: new[]
            {
                ("SyncDirection.cs",
                 "Is the type the invariant is about, so a member named inside it is the declaration rather than a second reading of it. The allowance is written for the file the rule exists to permit rather than left implicit, because a rule whose scope is every plugin source would otherwise refuse its own subject the moment that file carries an example in a remark."),
            },
            Regression: "if (request.Direction == SyncDirection.TwoWay)",
            RegressionIsTheMistakeThat: "asks which way a pairing moves at the place that happens to need the answer, which is how a direction check ends up in four places and how one of them keeps the old answer after the model gains a member.",
            NearMiss: "if (!Enum.IsDefined(configuration.Direction))",
            NearMissIsTheNeighbourThat: "is the legitimate reading the validator already makes: it asks whether the value is a direction this plugin declares at all and never which one it is, so it names no member and takes no branch on one.",
            CannotCatch: "a comparison that names no member. The value cast to its underlying number and compared against a literal spells none of this, and so does a direction obtained through a helper that returns a bool. It also has nothing to say about whether the one place that reads a direction reads it correctly, which is the second condition of #34 and needs a pass rather than a pattern."),

        new Rule(
            Id: "no-resolution-held-outside-the-call-that-derived-it",
            GuidePhrase: "no resolution held in a field or a property",
            Invariant: "A resolution is true of two libraries as they stood when it was computed, so nothing keeps one in a slot that outlives the call that derived it and a later pass cannot read one.",
            DeclaredBy: "#33",
            RefusedBy: nameof(ResolutionLifetimeTests),
            CannotCatch: "the same bounds that test states for itself, and one this row makes worse by sitting in a table of token scans: a resolution is a type rather than a spelling, so a pattern here would refuse the resolvers that return one. It carries no pattern for that reason and the walk is the whole mechanism."),
    };

    /// <summary>
    /// Gets the rule identifiers, so the theories below name a rule rather than
    /// carrying one through xunit's serialisation.
    /// </summary>
    public static TheoryData<string> RuleIds
    {
        get
        {
            var ids = new TheoryData<string>();
            foreach (var rule in Rules)
            {
                ids.Add(rule.Id);
            }

            return ids;
        }
    }

    /// <summary>
    /// Gets the identifiers of the rules this file refuses with a pattern of
    /// its own, which is every rule except the one already held elsewhere.
    /// </summary>
    public static TheoryData<string> PatternRuleIds
    {
        get
        {
            var ids = new TheoryData<string>();
            foreach (var rule in Rules.Where(rule => rule.RefusedBy is null))
            {
                ids.Add(rule.Id);
            }

            return ids;
        }
    }

    /// <summary>
    /// The lint reads files. If it ever reads none it passes for the wrong
    /// reason, so the file set is asserted before anything is concluded from a
    /// clean run.
    /// </summary>
    [Fact]
    public void ThePluginSourcesReachTheLint()
    {
        Assert.NotEmpty(PluginSourceFiles());
    }

    /// <summary>
    /// The clean side of the verification. Stated rather than implied: the
    /// plugin is a skeleton today and contains none of these spellings, so this
    /// passes over a tree that could hardly fail it and proves nothing about the
    /// patterns. What proves each pattern bites is the pair of theories below.
    /// This is here so the first line written into a resolution or a
    /// reconciliation path is judged by the rule instead of by whoever reviews
    /// that change.
    /// </summary>
    [Fact]
    public void NoRuleFindsAnythingInThePluginToday()
    {
        var sources = ReadPluginSources();
        var findings = Rules
            .Where(rule => rule.RefusedBy is null)
            .SelectMany(rule => Findings(rule, sources).Select(finding => rule.Id + " " + finding))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            findings.Count == 0,
            "The invariant lint refuses this tree: " + string.Join("; ", findings));
    }

    /// <summary>
    /// Every rule carries its record. A pattern with no invariant, no issue and
    /// no statement of its reach is the failure mode this issue names, so the
    /// suite refuses one rather than trusting that it was written.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuleIds))]
    public void EveryRuleNamesItsInvariantItsIssueAndWhatItCannotCatch(string id)
    {
        var rule = RuleNamed(id);

        Assert.False(string.IsNullOrWhiteSpace(rule.Invariant), id + " declares no invariant.");
        Assert.False(string.IsNullOrWhiteSpace(rule.CannotCatch), id + " does not say what it cannot catch.");
        Assert.Matches("^#[0-9]+$", rule.DeclaredBy);
    }

    /// <summary>
    /// The bite. Each pattern is run against the mistake it exists for, so a
    /// pattern that matches nothing at all fails here instead of passing
    /// quietly on a tree that has nothing to match.
    /// </summary>
    [Theory]
    [MemberData(nameof(PatternRuleIds))]
    public void EveryRuleRefusesItsOwnRegression(string id)
    {
        var rule = RuleNamed(id);

        Assert.NotEmpty(Findings(rule, Sample(rule.Regression)));
    }

    /// <summary>
    /// The neighbour. A pattern that refuses everything is as useless as one
    /// that refuses nothing, and each rule says in its own record which mistake
    /// its neighbour represents.
    /// </summary>
    [Theory]
    [MemberData(nameof(PatternRuleIds))]
    public void EveryRuleAcceptsItsOwnNearMiss(string id)
    {
        var rule = RuleNamed(id);

        Assert.Empty(Findings(rule, Sample(rule.NearMiss)));
    }

    /// <summary>
    /// A rule may say that something else already refuses its invariant, which
    /// keeps one spelling from being scanned for in two places. What it may not
    /// do is point at nothing, so the name it gives is resolved in this
    /// assembly.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuleIds))]
    public void ARuleHeldElsewhereNamesSomethingThatExists(string id)
    {
        var rule = RuleNamed(id);
        if (rule.RefusedBy is null)
        {
            Assert.NotEmpty(rule.Tokens);
            return;
        }

        var named = typeof(InvariantLintTests).Assembly
            .GetTypes()
            .Any(type => string.Equals(type.Name, rule.RefusedBy, StringComparison.Ordinal));

        Assert.True(named, id + " says " + rule.RefusedBy + " holds it, and no such type is in the suite.");
    }

    /// <summary>
    /// An allowance names a file that is in the tree and says why. A file name
    /// that matches nothing is a rule quietly narrowed against a file somebody
    /// renamed, and it reads exactly like one that is still doing its job.
    /// </summary>
    [Theory]
    [MemberData(nameof(RuleIds))]
    public void EveryAllowanceNamesAFileThatIsThereAndSaysWhy(string id)
    {
        var rule = RuleNamed(id);
        var files = PluginSourceFiles().Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);

        foreach (var (file, reason) in rule.Allowances)
        {
            Assert.Contains(file, files);
            Assert.True(reason.Length > 80, id + " allows " + file + " with a reason too short to argue with.");
        }
    }

    /// <summary>
    /// An allowance excuses a file and never the pattern. Run against text that
    /// is not one of the allowed files, every rule still refuses its own
    /// regression, which is the leg that would catch an allowance written as a
    /// blanket.
    /// </summary>
    [Theory]
    [MemberData(nameof(PatternRuleIds))]
    public void AnAllowanceDoesNotReachTextFromAnywhereElse(string id)
    {
        var rule = RuleNamed(id);

        Assert.NotEmpty(Findings(rule, new[] { ("SomeOtherFile.cs", rule.Regression) }));
    }

    /// <summary>
    /// The rule the contributing guide states about itself, made into a check
    /// rather than left to a reader. The guide lists the invariants of this
    /// shape in one sentence; this compares that list against the table above
    /// and fails in both directions, so an invariant added to the plan with no
    /// rule reds the suite and a rule here that the plan never declared does
    /// too.
    /// </summary>
    [Fact]
    public void TheRuleTableMatchesTheInvariantsTheGuideDeclares()
    {
        var declared = InvariantsDeclaredByTheGuide();
        var held = Rules.Select(rule => rule.GuidePhrase).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(declared, held);
    }

    /// <summary>
    /// Reads the guide's list of greppable invariants. The list is one sentence
    /// so that it can be read this way, and the marker is the phrase the
    /// sentence ends with before the list begins.
    /// </summary>
    private static IReadOnlyList<string> InvariantsDeclaredByTheGuide()
    {
        const string Marker = "a pattern a lint can refuse in a second:";

        var guide = Path.Combine(RepositoryRoot(), "CONTRIBUTING.md");
        Assert.True(File.Exists(guide), "The contributing guide is not at " + guide);

        var text = string.Join(' ', File.ReadAllLines(guide));
        var start = text.IndexOf(Marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "The guide no longer introduces its invariant list with: " + Marker);

        var rest = text[(start + Marker.Length)..];
        var end = rest.IndexOf('.', StringComparison.Ordinal);
        Assert.True(end >= 0, "The guide's invariant list does not end in a full stop.");

        return rest[..end]
            .Split(',')
            .Select(item => string.Join(' ', item.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(item => item.Length > 0)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Runs one rule over a set of named texts and returns what it refuses.
    /// Comment lines are skipped, because a comment explaining a rule is not a
    /// breach of it, and every record above says what that costs.
    /// </summary>
    private static IReadOnlyList<string> Findings(Rule rule, IReadOnlyList<(string Name, string Text)> sources)
    {
        var hits = new List<string>();

        foreach (var (name, text) in sources)
        {
            if (rule.Allowances.Any(allowed => string.Equals(allowed.File, name, StringComparison.Ordinal)))
            {
                continue;
            }

            var number = 0;
            foreach (var raw in text.Split('\n'))
            {
                number++;
                var line = raw.TrimEnd('\r').TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith('*'))
                {
                    continue;
                }

                if (rule.LiteralOnly && !line.Contains('"', StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var token in rule.Tokens.Where(token => line.Contains(token, StringComparison.Ordinal)))
                {
                    hits.Add(name + ":" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + token);
                }
            }
        }

        return rule.Kind == Refusal.EveryOccurrenceAfterTheFirst ? hits.Skip(1).ToList() : hits;
    }

    private static IReadOnlyList<(string Name, string Text)> Sample(string text)
    {
        return new[] { ("sample", text) };
    }

    private static Rule RuleNamed(string id)
    {
        var rule = Rules.SingleOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        Assert.NotNull(rule);
        return rule;
    }

    private static IReadOnlyList<(string Name, string Text)> ReadPluginSources()
    {
        return PluginSourceFiles()
            .Select(file => (Path.GetFileName(file), File.ReadAllText(file)))
            .ToList();
    }

    private static IReadOnlyList<string> PluginSourceFiles()
    {
        var pluginDirectory = Path.Combine(RepositoryRoot(), "Jellyfin.Plugin.MetadataSync");
        Assert.True(Directory.Exists(pluginDirectory), "Plugin sources not found at " + pluginDirectory);

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

    /// <summary>
    /// One rule. The pattern is the small half; the three sentences beside it
    /// are what let a reader decide what a green run is worth.
    /// </summary>
    private sealed record Rule(
        string Id,
        string GuidePhrase,
        string Invariant,
        string DeclaredBy,
        string CannotCatch,
        Refusal Kind = Refusal.AnyOccurrence,
        string[]? TokenPatterns = null,
        bool LiteralOnly = false,
        string Regression = "",
        string RegressionIsTheMistakeThat = "",
        string NearMiss = "",
        string NearMissIsTheNeighbourThat = "",
        string? RefusedBy = null,
        (string File, string Reason)[]? AllowedIn = null)
    {
        /// <summary>
        /// Gets the token patterns this rule refuses, which is empty for a rule
        /// held somewhere else.
        /// </summary>
        public string[] Tokens { get; } = TokenPatterns ?? Array.Empty<string>();

        /// <summary>
        /// Gets the files this rule does not read, each with the reason it does
        /// not. The record above promised this and carried none, so until now a
        /// legitimate use outside a rule's narrow path had nowhere to be
        /// declared and the only way past a rule was to weaken its pattern.
        /// </summary>
        public (string File, string Reason)[] Allowances { get; } = AllowedIn ?? Array.Empty<(string, string)>();
    }
}
