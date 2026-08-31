using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds <c>docs/refused-tests.md</c> to its own shape and its own arithmetic.
///
/// The register is where a test this suite will not carry is written down, and
/// its last section counts itself: how many entries it holds, and how many of
/// those are gaps. A gap is defined in the file's own opening as an entry whose
/// <c>Instead</c> is <c>nothing</c>, so the count is derivable from the entries
/// above the sentence that states it.
///
/// It was not derived, and it was wrong. The sentence said three of four
/// entries were gaps while two of them said <c>nothing</c>, and the same number
/// stood in <c>docs/testing.md</c> beside it. Both arrived in the change that
/// grew the register from one entry to four, whose own message says in one
/// paragraph that three of the new entries are gaps and in the next that only
/// the socket entry has a substitute. A reader deciding how much of this plugin
/// is covered reads that number, and a number nothing recomputes is one that
/// stops being true without anybody noticing.
///
/// The accounting was the whole of it, and a second half is here now. An
/// entry that is not a gap earns its way out of the gap count by naming a
/// test, and nothing resolved that name: the register carried
/// <c>PluginIdentityTests.ThisMethodDoesNotExistAnywhere</c> in place of a
/// substitute and every route stayed green, so a rename anywhere in this
/// suite could turn a covered property into an uncovered one with the file
/// still reading as covered. That is a negative disclosure becoming a
/// positive one by omission, which is the direction this register exists
/// against. The two legs below resolve every name it spells.
///
/// What this still does not reach is the argument. Whether the substitute an
/// entry names actually proves the property the refused test would have
/// proved is a judgement, and an entry naming a test that proves nothing
/// reads here exactly like one that proves everything. That is what the
/// review is for, and it is stated rather than left to be assumed.
/// </summary>
public class RefusedTestRegisterTests
{
    /// <summary>
    /// The register, copied to the output rather than located by walking up
    /// from the test binary, for the reason the field register is: walking up
    /// answers a different question on a machine where the tests run from
    /// somewhere else.
    /// </summary>
    private static readonly string _register = Path.Combine(AppContext.BaseDirectory, "refused-tests.md");

    /// <summary>
    /// The line that declares which of the four needs an entry is about.
    /// </summary>
    private const string Needs = "Needs: ";

    /// <summary>
    /// The line that declares what proves the same property in place of the
    /// refused test.
    /// </summary>
    private const string Instead = "Instead: ";

    /// <summary>
    /// What the lines of a section are joined with when the section is held as
    /// one string. A space, because the join exists so a name is read whole
    /// wherever the wrapping put it, and nothing below reads a line boundary.
    /// </summary>
    private const char Joiner = ' ';

    /// <summary>
    /// The value of <see cref="Instead"/> that makes an entry a gap, spelled as
    /// the register's own opening spells it.
    /// </summary>
    private const string Nothing = "nothing";

    /// <summary>
    /// The number words this renders, so the sentence reads as the rest of the
    /// document does. A count outside the table fails by name rather than
    /// silently rendering a digit into prose that carries none.
    /// </summary>
    private static readonly string[] _words =
    {
        "no", "one", "two", "three", "four", "five", "six", "seven", "eight",
        "nine", "ten", "eleven", "twelve"
    };

    /// <summary>
    /// One entry: the section heading, the two fields it declares, and the
    /// whole of the text under it.
    /// </summary>
    /// <param name="Title">The test that is refused, as the heading says it.</param>
    /// <param name="Needs">Which of the four needs it has, or null where it declares none.</param>
    /// <param name="Instead">What proves the property instead, or null where it declares none.</param>
    /// <param name="Body">Every line under the heading, so a name is found wherever the wrapping put it.</param>
    private sealed record Entry(string Title, string? Needs, string? Instead, string Body);

    /// <summary>
    /// One name the register spells for a test in this suite, and the entry it
    /// was spelled in.
    /// </summary>
    /// <param name="Title">The entry that names it.</param>
    /// <param name="Type">The type the name's second-to-last segment gives.</param>
    /// <param name="Method">The method the name's last segment gives.</param>
    private sealed record Reference(string Title, string Type, string Method);

    /// <summary>
    /// Every section that declares one of the two fields declares both. A
    /// section carrying a need and no substitute is an entry that drops out of
    /// the gap count while reading as an entry, which is the one way the count
    /// below can be right about a register that is wrong.
    /// </summary>
    [Fact]
    public void EveryEntryDeclaresWhatItNeedsAndWhatProvesThePropertyInstead()
    {
        var half = Sections()
            .Where(e => (e.Needs is null) != (e.Instead is null))
            .Select(e => e.Title)
            .ToList();

        Assert.Empty(half);
    }

    /// <summary>
    /// The closing sentence is rendered from the entries and compared, rather
    /// than searched for. A sentence that is looked for is found or not found;
    /// one that is rendered cannot be true about a register it does not
    /// describe.
    /// </summary>
    [Fact]
    public void TheRegisterCountsItsOwnEntriesAndItsOwnGaps()
    {
        var entries = Entries();
        var gaps = entries.Count(IsGap);

        Assert.Contains(Sentence(entries.Count, gaps), Collapsed(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The reading finds a register with entries of both kinds in it. Without
    /// this leg a reader that matched nothing would leave the shape assertion
    /// above green over an empty set, which is the direction that fails open:
    /// a parser that has stopped finding entries reads exactly like a register
    /// in which every entry is well formed.
    /// </summary>
    [Fact]
    public void TheReadingFindsTheEntriesThatAreThere()
    {
        var entries = Entries();

        Assert.True(entries.Count > 1, "The reading found " + Count(entries.Count) + " entries in the register.");
        Assert.Contains(entries, IsGap);
        Assert.Contains(entries, e => !IsGap(e));
    }

    /// <summary>
    /// Every test this register names is a test this suite runs. A name that
    /// resolves to nothing is the register saying a property is covered by
    /// something that is not there, which is the one way an entry can leave the
    /// gap count without the gap closing.
    /// </summary>
    [Fact]
    public void EverySubstituteThisRegisterNamesIsATestThisSuiteRuns()
    {
        var missing = References()
            .Where(r => Resolve(r) is null)
            .Select(r => r.Type + "." + r.Method + ", named under: " + r.Title)
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// Every entry that is not a gap names at least one test the reading above
    /// found. Without this leg the reading fails open: a substitute written
    /// outside backticks, or with a lower-case first letter, drops out of the
    /// population and leaves the resolution leg green over a smaller set than
    /// the register holds.
    /// </summary>
    [Fact]
    public void EveryEntryThatIsNotAGapNamesATestTheReadingFinds()
    {
        var references = References();

        var unread = Entries()
            .Where(e => !IsGap(e))
            .Where(e => !references.Any(r => string.Equals(r.Title, e.Title, StringComparison.Ordinal)))
            .Select(e => e.Title)
            .ToList();

        Assert.Empty(unread);
    }

    /// <summary>
    /// The sentence the register closes with, for a given count of entries and
    /// of gaps.
    /// </summary>
    /// <param name="entries">How many entries the register holds.</param>
    /// <param name="gaps">How many of them are gaps.</param>
    /// <returns>The sentence, as the document carries it.</returns>
    private static string Sentence(int entries, int gaps)
    {
        var held = entries == 1
            ? "This register holds one entry"
            : "This register holds " + Count(entries) + " entries";

        var are = gaps == 1
            ? " and one of them is a gap."
            : " and " + Count(gaps) + " of them are gaps.";

        return held + are;
    }

    /// <summary>
    /// A count as the document spells one.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <returns>The number word.</returns>
    private static string Count(int count)
    {
        Assert.InRange(count, 0, _words.Length - 1);

        return _words[count];
    }

    /// <summary>
    /// Whether an entry is a gap, by the definition the register's own opening
    /// gives: its substitute is <c>nothing</c>.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>Whether it is a gap.</returns>
    private static bool IsGap(Entry entry) =>
        string.Equals(entry.Instead, Nothing, StringComparison.Ordinal);

    /// <summary>
    /// The method a name resolves to, or null where this suite carries no such
    /// test. A method that exists and carries no fact attribute resolves to
    /// null as well: the register names what proves a property, and a member
    /// nothing runs proves none. <c>TheoryAttribute</c> derives from
    /// <c>FactAttribute</c>, so both are reached by the one test.
    ///
    /// The type is matched on its simple name, because that is how the register
    /// spells one. Two types of that name in two namespaces would be resolved
    /// to whichever the walk reached first; this suite declares one namespace,
    /// and the bound is stated rather than left to be assumed.
    /// </summary>
    /// <param name="reference">The name the register spells.</param>
    /// <returns>The method, or null.</returns>
    private static MethodInfo? Resolve(Reference reference)
    {
        var type = typeof(RefusedTestRegisterTests).Assembly
            .GetTypes()
            .FirstOrDefault(t => string.Equals(t.Name, reference.Type, StringComparison.Ordinal));

        var method = type?.GetMethod(
            reference.Method,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        return method?.GetCustomAttributes().Any(a => a is FactAttribute) == true ? method : null;
    }

    /// <summary>
    /// Every name the register spells for a test in this suite. A name is a
    /// backticked token of dot-separated segments where every segment begins
    /// with an upper-case letter, which is how this suite spells a type and a
    /// method and is not how a file name is spelled, so <c>testing.md</c> beside
    /// one is not read as a test.
    ///
    /// It reads the whole of a section rather than the <c>Instead</c> line, so a
    /// name that a re-wrap moved onto the next line is still read.
    /// </summary>
    /// <returns>The names, each with the entry that spelled it.</returns>
    private static List<Reference> References() =>
        Sections()
            .SelectMany(section => Backticked(section.Body)
                .Select(token => token.Split('.'))
                .Where(segments => segments.Length >= 2 && segments.All(IsUpperCamel))
                .Select(segments => new Reference(section.Title, segments[^2], segments[^1])))
            .ToList();

    /// <summary>
    /// The spans of text between backticks, which are the odd-numbered pieces
    /// of a split on the backtick.
    /// </summary>
    /// <param name="text">The text to read.</param>
    /// <returns>The backticked spans.</returns>
    private static IEnumerable<string> Backticked(string text)
    {
        var pieces = text.Split('`');

        for (var i = 1; i < pieces.Length; i += 2)
        {
            yield return pieces[i];
        }
    }

    /// <summary>
    /// Whether a segment is spelled the way this suite spells a type or a
    /// method: an upper-case letter, then letters, digits and underscores.
    /// </summary>
    /// <param name="segment">The segment.</param>
    /// <returns>Whether it is spelled that way.</returns>
    private static bool IsUpperCamel(string segment) =>
        segment.Length > 0
        && char.IsAsciiLetterUpper(segment[0])
        && segment.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    /// <summary>
    /// The sections that are entries, which are the ones declaring both fields.
    /// </summary>
    /// <returns>The entries.</returns>
    private static List<Entry> Entries() =>
        Sections().Where(e => e.Needs is not null && e.Instead is not null).ToList();

    /// <summary>
    /// Every section of the document, with whichever of the two fields it
    /// declares and the whole of the text under it. The closing section
    /// declares neither and is not an entry; nothing here names it, so a
    /// register whose last section is renamed is read the same way.
    ///
    /// Text above the first heading is in no section, which is where the file
    /// states how it is read rather than stating anything about a test.
    /// </summary>
    /// <returns>The sections.</returns>
    private static List<Entry> Sections()
    {
        var sections = new List<Entry>();
        var title = string.Empty;
        string? needs = null;
        string? instead = null;
        var body = new List<string>();
        var open = false;

        foreach (var line in Lines())
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (open)
                {
                    sections.Add(new Entry(title, needs, instead, string.Join(Joiner, body)));
                }

                title = line[3..];
                needs = null;
                instead = null;
                body = new List<string>();
                open = true;
                continue;
            }

            if (!open)
            {
                continue;
            }

            body.Add(line);

            if (line.StartsWith(Needs, StringComparison.Ordinal))
            {
                needs = line[Needs.Length..];
            }
            else if (line.StartsWith(Instead, StringComparison.Ordinal))
            {
                instead = line[Instead.Length..];
            }
        }

        if (open)
        {
            sections.Add(new Entry(title, needs, instead, string.Join(Joiner, body)));
        }

        return sections;
    }

    /// <summary>
    /// The document's lines, with the line ending normalised so the reading is
    /// the same on either platform, and each line trimmed of trailing space so
    /// an invisible byte is not the difference between a match and a failure.
    /// </summary>
    /// <returns>The lines.</returns>
    private static List<string> Lines() =>
        Text().Split('\n').Select(l => l.TrimEnd()).ToList();

    /// <summary>
    /// The whole document as one line, so a sentence is compared against what
    /// it says rather than against where the wrapping happens to break it. A
    /// paragraph re-wrapped by an editor carries the same claim, and this reads
    /// it as the same claim.
    /// </summary>
    /// <returns>The text, with every run of whitespace collapsed to one space.</returns>
    private static string Collapsed() =>
        string.Join(' ', Text().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// The document, with the line ending normalised so the reading is the same
    /// on either platform.
    /// </summary>
    /// <returns>The text.</returns>
    private static string Text() =>
        File.ReadAllText(_register).Replace("\r\n", "\n", StringComparison.Ordinal);
}
