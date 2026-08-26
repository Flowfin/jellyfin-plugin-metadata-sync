using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Jellyfin.Plugin.MetadataSync.Store;

/// <summary>
/// What this plugin holds for one pairing, across every store it owns.
/// </summary>
/// <remarks>
/// This is the answer to an operator asking what a plugin knows about one
/// relationship, and it is the same object a confirmation before a removal is
/// built from, because those are one question asked before and after a decision.
/// <para>
/// <see cref="Document"/> is the export. It is produced rather than written: a
/// document handed to whoever asked for it is a thing an operator saves where
/// they choose, and a plugin writing it into its own data folder would leave the
/// most personal artefact it ever produces lying beside the store with nothing to
/// clean it up. Which surface hands it over is #51.
/// </para>
/// <para>
/// What the document may carry is bounded by <c>docs/personal-data.md</c> and it
/// is not a diagnostic dump. A previous value of a free-text field can carry a
/// sentence somebody wrote about their household, so the document says what it is
/// carrying before it carries any of it, in the first lines rather than at the
/// end.
/// </para>
/// </remarks>
public sealed class PairingReport
{
    /// <summary>
    /// Gets the pairing this is about.
    /// </summary>
    public required Guid PairingId { get; init; }

    /// <summary>
    /// Gets what each store holds, one entry per store, including the stores
    /// that hold nothing.
    /// </summary>
    public required IReadOnlyList<PairingHolding> Holdings { get; init; }

    /// <summary>
    /// Gets how many rows are held across every store.
    /// </summary>
    public int Count => Holdings.Sum(holding => holding.Count);

    /// <summary>
    /// The report as a document, carrying every row it counted.
    /// </summary>
    /// <returns>The document.</returns>
    /// <remarks>
    /// Every counted row appears in it. A report stating a count and handing over
    /// a summary lets an operator believe they have been given what the plugin
    /// holds when they have been given a number.
    /// </remarks>
    public string Document()
    {
        var text = new StringBuilder();

        text.Append(CultureInfo.InvariantCulture, $"What this plugin holds for pairing {PairingId}").Append('\n').Append('\n');

        text.Append("This is what one plugin on this server kept about one pairing. Some of it\n");
        text.Append("is text somebody typed into a library, the values that were replaced among\n");
        text.Append("it, so treat this as you would the library rather than as a diagnostic\n");
        text.Append("file.\n").Append('\n');

        text.Append("Removing these records does not change the library. Metadata this plugin\n");
        text.Append("already wrote stays on the items it was written to, and putting a value\n");
        text.Append("back is a different act from this one.\n").Append('\n');

        foreach (var holding in Holdings)
        {
            text.Append(CultureInfo.InvariantCulture, $"{holding.Store}: {holding.Count} row(s), {holding.Held}").Append('\n');

            foreach (var row in holding.Rows)
            {
                text.Append("    ").Append(row).Append('\n');
            }

            text.Append('\n');
        }

        text.Append(CultureInfo.InvariantCulture, $"{Count} row(s) in total, across {Holdings.Count} store(s).").Append('\n');

        return text.ToString();
    }
}
