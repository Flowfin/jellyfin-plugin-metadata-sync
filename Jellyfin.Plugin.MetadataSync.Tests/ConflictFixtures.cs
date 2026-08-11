using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.MetadataSync.Conflicts;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The fixture table out of <c>docs/conflicts.md</c>, read once for everybody
/// who runs against it.
/// </summary>
/// <remarks>
/// The rows live in the document rather than here on purpose: the cases are the
/// argument for the rule set, and an argument written in a test file is one a
/// reader has to compile to read. This type is the reader and nothing more, so
/// a row edited in the document reaches every test that uses it.
/// <para>
/// It parses strictly. A cell it does not understand fails here rather than
/// turning into a fixture that quietly tests something else, because a fixture
/// table is the one place where a silently wrong input produces a green run
/// that means nothing.
/// </para>
/// </remarks>
internal static class ConflictFixtures
{
    private const string FixtureHeader = "| Case | This server | The peer | This plugin last wrote | Locks | Rule | Outcome | The mistake it would catch |";

    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "conflicts.md");

    /// <summary>
    /// Gets the name of every case, so a theory names a case rather than
    /// carrying a row through xunit's serialisation.
    /// </summary>
    public static TheoryData<string> CaseNames
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var row in Rows())
            {
                names.Add(row[0]);
            }

            return names;
        }
    }

    /// <summary>
    /// Every row of the fixture table, as the document writes it.
    /// </summary>
    /// <returns>The rows, each split into its eight cells.</returns>
    public static IReadOnlyList<string[]> Rows() => RowsUnder(FixtureHeader, 8);

    /// <summary>
    /// The mistake a row says it would catch.
    /// </summary>
    /// <remarks>
    /// It is a cell rather than a paragraph beside the table because a paragraph
    /// covers the rows somebody remembered to mention, and the rows it leaves
    /// out are the ones nobody argued for. A cell is owed by every row, and the
    /// suite refuses one that is empty.
    /// </remarks>
    /// <param name="row">The row.</param>
    /// <returns>The sentence.</returns>
    public static string MistakeFor(string[] row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row[7];
    }

    /// <summary>
    /// Every row of one hand-written table in the document, found by its header
    /// line.
    /// </summary>
    /// <remarks>
    /// The document carries more than one table the suite reads, and they are
    /// parsed here rather than each in its own reader, so the strictness below
    /// is one decision instead of several that drift apart. A header nobody
    /// finds fails rather than returning nothing, because a table renamed in the
    /// document would otherwise turn every test over it into a test over an
    /// empty set.
    /// </remarks>
    /// <param name="header">The table's header line, as the document writes it.</param>
    /// <param name="cells">How many cells a row of that table has.</param>
    /// <returns>The rows, each split into its cells.</returns>
    public static IReadOnlyList<string[]> RowsUnder(string header, int cells)
    {
        var lines = File.ReadAllLines(_document);
        var start = Array.FindIndex(lines, l => string.Equals(l.Trim(), header, StringComparison.Ordinal));
        Assert.True(start >= 0, "The document has no table headed " + header);

        var rows = new List<string[]>();
        for (var i = start + 2; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('|'))
            {
                break;
            }

            var split = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            Assert.Equal(cells, split.Length);
            rows.Add(split);
        }

        Assert.NotEmpty(rows);
        return rows;
    }

    /// <summary>
    /// The row a case name belongs to.
    /// </summary>
    /// <param name="caseName">The case, as the document's first column writes it.</param>
    /// <returns>The row.</returns>
    public static string[] Named(string caseName)
    {
        var row = Rows().SingleOrDefault(r => string.Equals(r[0], caseName, StringComparison.Ordinal));
        Assert.NotNull(row);
        return row;
    }

    /// <summary>
    /// What the resolver is handed for one row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The inputs the row describes.</returns>
    public static ConflictInputs InputsFor(string[] row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var locks = row[4];

        return new ConflictInputs
        {
            LocalValue = Value(row[1]),
            PeerValue = Value(row[2]),
            LastWrittenByThisPlugin = Value(row[3]),
            ItemLockedHere = string.Equals(locks, "item here", StringComparison.Ordinal),
            FieldLockedHere = string.Equals(locks, "field here", StringComparison.Ordinal),
            FieldLockedOnPeer = string.Equals(locks, "field on the peer", StringComparison.Ordinal),
        };
    }

    /// <summary>
    /// The rule a row says answers it, or null where it says none does.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The rule name, or null for the residual.</returns>
    public static string? RuleIdFor(string[] row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row[5].Length == 0 ? null : Unquote(row[5]);
    }

    /// <summary>
    /// The outcome a row expects.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The outcome.</returns>
    public static ConflictOutcome OutcomeFor(string[] row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var name = Unquote(row[6]);
        Assert.Contains(name, Enum.GetNames<ConflictOutcome>(), StringComparer.Ordinal);
        return Enum.Parse<ConflictOutcome>(name, ignoreCase: false);
    }

    /// <summary>
    /// Takes a name out of the backticks the document writes it in.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <returns>The name.</returns>
    public static string Unquote(string cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        return cell.Trim().Trim('`');
    }

    /// <summary>
    /// Takes a value out of the quotes the document writes it in. An empty cell
    /// is no value at all, which is a different thing from an empty string and
    /// is the distinction two of the rules turn on.
    /// </summary>
    /// <remarks>
    /// Nothing is trimmed inside the quotes. A row exists whose whole point is
    /// that the peer's value is a single space, and a parser that tidied it
    /// would hand the rule set the case next door.
    /// </remarks>
    private static string? Value(string cell)
    {
        if (cell.Length == 0)
        {
            return null;
        }

        Assert.StartsWith("`\"", cell, StringComparison.Ordinal);
        Assert.EndsWith("\"`", cell, StringComparison.Ordinal);

        return cell[2..^2];
    }
}
