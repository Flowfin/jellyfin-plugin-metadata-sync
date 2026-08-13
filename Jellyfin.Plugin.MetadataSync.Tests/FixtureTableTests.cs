using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Every fixture table in the documents is run by somebody, and every row of it
/// reaches a theory.
/// </summary>
/// <remarks>
/// The fixture tables live in the documents because the cases are the argument
/// for the rules, and each has a reader in this suite that turns its rows into
/// theory data. Nothing held the two together. A table added to a document with
/// no reader is a table nobody runs, and it reads exactly like coverage: rows in
/// a document, each naming the mistake it would catch, none of them executed by
/// anything.
/// <para>
/// The pairing is by document and header together rather than by either alone.
/// Two of the four tables carry the same header in two different documents, so a
/// reader matched on the header would be satisfied by the wrong table.
/// </para>
/// <para>
/// What this counts and what it does not. It reads a table's rows and never its
/// cells, so it is not a fifth copy of the cell readers each fixture type
/// carries. And it proves a row reaches a theory's data, never that the theory
/// asserts anything about it: a theory taking every row and asserting nothing
/// passes here. What each row is worth once it arrives is the fixture type's own
/// business, and every one of them fails on a cell it does not understand.
/// </para>
/// <para>
/// It finds a fixture table by the shape of its header, which is the one thing
/// every such table has in common. A table headed differently is invisible to
/// it, and that is the direction that fails open. The reverse leg is what stops
/// it being silent: a reader naming a header no document carries reds, so a
/// header edited out of a document is caught even though the scan can no longer
/// see the table.
/// </para>
/// <para>
/// The documents are read out of the repository rather than out of the copies
/// beside the test binary. A fixture table in a document nobody copied to the
/// output is exactly the case this exists for, and a scan of the output could
/// not see one.
/// </para>
/// </remarks>
public class FixtureTableTests
{
    /// <summary>
    /// How every fixture table in this tree heads its first column. The tables
    /// differ in every other column and agree on this one.
    /// </summary>
    private const string FixtureHeaderStart = "| Case |";

    /// <summary>
    /// The scan reads documents. If it ever reads none, or finds no table in
    /// any of them, it passes for the wrong reason, so both are asserted before
    /// anything is concluded from a clean run.
    /// </summary>
    [Fact]
    public void TheDocumentsAndTheirTablesReachTheScan()
    {
        Assert.NotEmpty(Documents());
        Assert.NotEmpty(TablesInTheDocuments());
    }

    /// <summary>
    /// A reader is a type declaring the document it reads and the header it
    /// reads under. If it ever finds none, every other leg here is vacuous, so
    /// the set is asserted rather than assumed.
    /// </summary>
    [Fact]
    public void TheReadersReachTheScan()
    {
        Assert.NotEmpty(Readers());
    }

    /// <summary>
    /// The direction that catches a fixture table nobody runs.
    /// </summary>
    [Fact]
    public void EveryFixtureTableInTheDocumentsIsReadBySomebody()
    {
        var unread = TablesInTheDocuments()
            .Where(table => !Readers().Any(reader =>
                string.Equals(reader.Document, table.Document, StringComparison.Ordinal)
                && string.Equals(reader.Header, table.Header, StringComparison.Ordinal)))
            .Select(table => table.Document + " " + table.Header)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unread.Count == 0,
            "A fixture table in the documents reaches no reader in this suite: " + string.Join("; ", unread));
    }

    /// <summary>
    /// The direction that catches a reader quietly reading nothing. A header
    /// edited in a document leaves the reader matching no table, and a fixture
    /// type that fails to find its table is a set of cases that stopped running
    /// without anybody deciding to stop them.
    /// </summary>
    [Fact]
    public void EveryReaderNamesATableThatIsInTheDocumentItReads()
    {
        var missing = Readers()
            .Where(reader => !TablesInTheDocuments().Any(table =>
                string.Equals(reader.Document, table.Document, StringComparison.Ordinal)
                && string.Equals(reader.Header, table.Header, StringComparison.Ordinal)))
            .Select(reader => reader.Name + " reads " + reader.Document + " under " + reader.Header)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "A reader names a table its document does not carry: " + string.Join("; ", missing));
    }

    /// <summary>
    /// Every row reaches a theory. A reader that dropped rows on the way, or
    /// stopped short of the end of its table, would leave cases in the document
    /// that nothing runs while the table and the reader both still exist.
    /// </summary>
    /// <remarks>
    /// Where a reader offers more than one set of theory data, one of them
    /// carrying every row is enough. The question here is whether a row reaches
    /// a theory at all, and a reader that also offers a narrower set is
    /// answering a different question beside it.
    /// </remarks>
    [Fact]
    public void EveryRowOfEveryFixtureTableReachesATheory()
    {
        var shortfalls = new List<string>();

        foreach (var reader in Readers())
        {
            var rows = TablesInTheDocuments()
                .Where(table => string.Equals(reader.Document, table.Document, StringComparison.Ordinal)
                    && string.Equals(reader.Header, table.Header, StringComparison.Ordinal))
                .Select(table => (int?)table.Rows)
                .FirstOrDefault();

            if (rows is null)
            {
                // The leg above says which reader this is and why. Reporting it
                // twice would name one fault in two places.
                continue;
            }

            var offered = TheoryDataCounts(reader.Type);
            Assert.True(offered.Count > 0, reader.Name + " reads a fixture table and offers no theory data.");

            if (!offered.Contains(rows.Value))
            {
                shortfalls.Add(
                    reader.Name + " runs " + string.Join("/", offered)
                    + " of the " + rows.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " rows in " + reader.Document);
            }
        }

        Assert.True(
            shortfalls.Count == 0,
            "A fixture table has rows that reach no theory: " + string.Join("; ", shortfalls));
    }

    /// <summary>
    /// The sizes of the theory data a reader offers, one per public static
    /// member that produces some.
    /// </summary>
    private static IReadOnlyCollection<int> TheoryDataCounts(Type reader)
    {
        const BindingFlags Where = BindingFlags.Public | BindingFlags.Static;

        var offered = reader.GetProperties(Where)
            .Where(property => typeof(IEnumerable<object[]>).IsAssignableFrom(property.PropertyType))
            .Select(property => property.GetValue(null))
            .Concat(reader.GetMethods(Where)
                .Where(method => !method.IsSpecialName
                    && method.GetParameters().Length == 0
                    && typeof(IEnumerable<object[]>).IsAssignableFrom(method.ReturnType))
                .Select(method => method.Invoke(null, null)));

        return offered
            .OfType<IEnumerable<object[]>>()
            .Select(data => data.Count())
            .ToList();
    }

    /// <summary>
    /// Every type in this suite that declares both the document it reads and
    /// the header it reads under, which is what a fixture reader is.
    /// </summary>
    private static IReadOnlyList<(Type Type, string Name, string Document, string Header)> Readers()
    {
        const BindingFlags Declared =
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var readers = new List<(Type, string, string, string)>();

        foreach (var type in typeof(FixtureTableTests).Assembly.GetTypes())
        {
            var header = type.GetField("FixtureHeader", Declared);
            var document = type.GetField("_document", Declared);
            if (header is null || document is null)
            {
                continue;
            }

            var headerValue = header.IsLiteral ? header.GetRawConstantValue() : header.GetValue(null);
            if (headerValue is not string headerText || document.GetValue(null) is not string documentPath)
            {
                continue;
            }

            readers.Add((type, type.Name, Path.GetFileName(documentPath), headerText.Trim()));
        }

        return readers;
    }

    /// <summary>
    /// Every fixture table in the documents, with the number of rows under it.
    /// </summary>
    private static IReadOnlyList<(string Document, string Header, int Rows)> TablesInTheDocuments()
    {
        var tables = new List<(string, string, int)>();

        foreach (var document in Documents())
        {
            var lines = File.ReadAllLines(document);
            for (var i = 0; i < lines.Length; i++)
            {
                var header = lines[i].Trim();
                if (!header.StartsWith(FixtureHeaderStart, StringComparison.Ordinal))
                {
                    continue;
                }

                var rows = 0;
                for (var j = i + 2; j < lines.Length && lines[j].TrimStart().StartsWith('|'); j++)
                {
                    rows++;
                }

                tables.Add((Path.GetFileName(document), header, rows));
            }
        }

        return tables;
    }

    private static IReadOnlyList<string> Documents()
    {
        var directory = Path.Combine(RepositoryRoot(), "docs");
        Assert.True(Directory.Exists(directory), "The documents are not at " + directory);

        return Directory.GetFiles(directory, "*.md", SearchOption.AllDirectories);
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
}
