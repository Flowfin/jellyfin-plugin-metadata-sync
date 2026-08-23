using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// What this reaches is the accounting and never the argument. Whether the
/// substitute an entry names actually proves the property the refused test
/// would have proved is a judgement, and an entry naming a substitute that
/// proves nothing reads here exactly like one that proves everything. That is
/// what the review is for, and it is stated rather than left to be assumed.
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
    /// One entry: the section heading, and the two fields it declares.
    /// </summary>
    /// <param name="Title">The test that is refused, as the heading says it.</param>
    /// <param name="Needs">Which of the four needs it has, or null where it declares none.</param>
    /// <param name="Instead">What proves the property instead, or null where it declares none.</param>
    private sealed record Entry(string Title, string? Needs, string? Instead);

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
    /// The sections that are entries, which are the ones declaring both fields.
    /// </summary>
    /// <returns>The entries.</returns>
    private static List<Entry> Entries() =>
        Sections().Where(e => e.Needs is not null && e.Instead is not null).ToList();

    /// <summary>
    /// Every section of the document, with whichever of the two fields it
    /// declares. The closing section declares neither and is not an entry;
    /// nothing here names it, so a register whose last section is renamed is
    /// read the same way.
    /// </summary>
    /// <returns>The sections.</returns>
    private static List<Entry> Sections()
    {
        var sections = new List<Entry>();
        var title = string.Empty;
        string? needs = null;
        string? instead = null;
        var open = false;

        foreach (var line in Lines())
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (open)
                {
                    sections.Add(new Entry(title, needs, instead));
                }

                title = line[3..];
                needs = null;
                instead = null;
                open = true;
                continue;
            }

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
            sections.Add(new Entry(title, needs, instead));
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
