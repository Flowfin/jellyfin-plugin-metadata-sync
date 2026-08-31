using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.MetadataSync.Fields;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// The published register is derived from the same file the plugin runs on, and
/// this is what keeps that true after the day it was written.
/// </summary>
/// <remarks>
/// The check regenerates rather than searches. A document can name every field,
/// carry every row and still describe them wrongly, which is what a containment
/// assertion passes: it finds what it looks for and never notices the sentence
/// beside it. What is compared here is the whole of each rendered block,
/// character for character, against what the source produces today.
/// <para>
/// The prose around the blocks is written by hand and nothing here derives it.
/// That is the bound on this check and it is stated rather than left to be
/// discovered: a paragraph that contradicts a table it sits next to is caught by
/// a reader and by nothing in this file.
/// </para>
/// <para>
/// It is a test and not a build step because the build this repository runs is
/// a reusable workflow in another repository, which cannot be given a
/// generation step from here. As a test the failure arrives with the rest of
/// the suite, on every route that runs it, and needs no new mechanism.
/// </para>
/// </remarks>
public class PublishedRegisterTests
{
    private static readonly string _document = Path.Combine(AppContext.BaseDirectory, "field-register.md");

    /// <summary>
    /// The committed document is exactly what the source produces. Both
    /// directions: a row added to the source with no rendering fails, and a
    /// line edited in the document that the source does not produce fails too.
    /// </summary>
    [Fact]
    public void TheCommittedDocumentIsWhatTheSourceProduces()
    {
        var text = File.ReadAllText(_document).Replace("\r\n", "\n", StringComparison.Ordinal);

        foreach (var (name, produced) in Rendered())
        {
            var committed = BlockIn(text, name);

            // Named per block rather than compared as one document, so a failure
            // says which table drifted instead of printing the whole file.
            Assert.Equal(produced, committed);
        }
    }

    /// <summary>
    /// Every block the source produces is actually in the document. A rendering
    /// nobody pasted in would otherwise pass the comparison above by never
    /// being compared.
    /// </summary>
    [Fact]
    public void EveryRenderedBlockIsPresentAndNoOtherIs()
    {
        var text = File.ReadAllText(_document).Replace("\r\n", "\n", StringComparison.Ordinal);

        var inDocument = text.Split('\n')
            .Where(l => l.StartsWith("<!-- rendered from field-register.json: ", StringComparison.Ordinal))
            .Select(l => l["<!-- rendered from field-register.json: ".Length..].Replace(" -->", string.Empty, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Rendered().Keys.Order(StringComparer.Ordinal).ToList(), inDocument);
    }

    /// <summary>
    /// The sentence written for an operator is not the sentence written for
    /// whoever maintains the row. A row where the two are the same has one of
    /// its two audiences wrong, and copying the register's own reason across
    /// is the cheap way to satisfy a column that asked for a second one.
    /// </summary>
    [Fact]
    public void EveryRowAnswersTheOperatorInItsOwnSentence()
    {
        var missing = FieldRegister.Rows
            .Where(r => string.IsNullOrWhiteSpace(r.OperatorReason) || r.OperatorReason.Length < 40)
            .Select(r => r.Field)
            .ToList();

        var copied = FieldRegister.Rows
            .Where(r => string.Equals(r.OperatorReason, r.Reason, StringComparison.Ordinal))
            .Select(r => r.Field)
            .ToList();

        // The register's own reasons cite issues and decisions by number, which
        // is the shape that says a sentence was written for the wrong reader. It
        // is a spelling and not an intent, so it catches the copy and not every
        // way of writing a bad sentence.
        var registerVoice = FieldRegister.Rows
            .Where(r => r.OperatorReason.Contains('#', StringComparison.Ordinal))
            .Select(r => r.Field)
            .ToList();

        Assert.Empty(missing);
        Assert.Empty(copied);
        Assert.Empty(registerVoice);
    }

    /// <summary>
    /// Renders every block the document carries, keyed by the name the document
    /// marks it with.
    /// </summary>
    private static Dictionary<string, string> Rendered()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rows"] = Table(
                "| Field | Item kinds | Moves | Class | From the file | Reason |",
                "| --- | --- | --- | --- | --- | --- |",
                FieldRegister.Rows.Select(r => string.Format(
                    CultureInfo.InvariantCulture,
                    "| `{0}` | {1} | {2} | {3} | {4} | {5} |",
                    r.Field,
                    r.KindGroup,
                    YesNo(r.Moves),
                    r.Class,
                    YesNo(r.FromTheFile),
                    r.Reason))),

            ["kind groups"] = Table(
                "| Group | Item kinds |",
                "| --- | --- |",
                FieldRegister.KindGroups.Select(g => string.Format(
                    CultureInfo.InvariantCulture,
                    "| `{0}` | {1} |",
                    g.Key,
                    string.Join(", ", g.Value.Select(k => "`" + k + "`"))))),

            ["where each field lives"] = Table(
                "| Field | Declared on | Reached through |",
                "| --- | --- |  --- |",
                FieldRegister.Rows.Select(r => string.Format(
                    CultureInfo.InvariantCulture,
                    "| `{0}` | {1} | {2} |",
                    r.Field,
                    r.DeclaredOn is null ? "not an item property" : "`" + r.DeclaredOn + "`",
                    r.ReachedBy is null ? "-" : "`" + r.ReachedBy + "`"))),

            ["what it means for an operator"] = Table(
                "| Field | Does it travel | What that means for your library |",
                "| --- | --- | --- |",
                FieldRegister.Rows.Select(r => string.Format(
                    CultureInfo.InvariantCulture,
                    "| `{0}` | {1} | {2} |",
                    r.Field,
                    YesNo(r.Moves),
                    r.OperatorReason))),

            ["locks"] = Table(
                "| Field | The lock that governs it |",
                "| --- | --- |",
                FieldRegister.Rows.Select(r => string.Format(
                    CultureInfo.InvariantCulture,
                    "| `{0}` | {1} |",
                    r.Field,
                    r.Lock is null ? "the item-level lock only" : "`MetadataField." + r.Lock + "`"))),
        };
    }

    private static string Table(string header, string rule, IEnumerable<string> rows)
    {
        var text = new StringBuilder(header).Append('\n').Append(rule);
        foreach (var row in rows)
        {
            text.Append('\n').Append(row);
        }

        return text.ToString();
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    /// <summary>
    /// Reads back the block the document marks with a name, so a failure
    /// compares one table against one table.
    /// </summary>
    private static string BlockIn(string text, string name)
    {
        var opening = "<!-- rendered from field-register.json: " + name + " -->\n";
        var start = text.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, $"The document carries no block named '{name}'.");

        start += opening.Length;
        var end = text.IndexOf("\n<!-- end rendered -->", start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"The block named '{name}' is not closed.");

        return text[start..end];
    }
}
