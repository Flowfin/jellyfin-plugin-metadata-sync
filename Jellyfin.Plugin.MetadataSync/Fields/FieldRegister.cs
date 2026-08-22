using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.MetadataSync.Fields;

/// <summary>
/// The declared set of fields, read from the register that ships inside the
/// assembly. This is the only place a field is declared: the document under
/// docs/ is checked against this file by the suite, and the planner asks this
/// register before a field reaches a plan.
/// </summary>
/// <remarks>
/// The register is data rather than code so that adding a field is adding a row
/// somebody has to argue for in a reason column, instead of adding a line to a
/// switch nobody reads twice. It is an embedded resource rather than a file
/// beside the assembly because a plugin directory is a place an operator can
/// edit, and a register an operator can edit is not a register.
/// <para>
/// The resource name and the text are both parameters on the internal seams
/// below, so every refusal in the loader is a line a test can reach. A guard
/// nothing can trip is indistinguishable from one that does not work.
/// </para>
/// <para>
/// This type reads a stream out of its own assembly and touches no file. It
/// names no file-system type, which is also what keeps the invariant lint
/// quiet, and that is worth saying out loud: the lint matches a spelling, the
/// rule it carries is about deciding item identity, and reading an embedded
/// resource is outside that rule rather than hidden from it. The rule table has
/// no allowed set to record the exemption in today, so this paragraph is where
/// it is recorded.
/// </para>
/// </remarks>
public static class FieldRegister
{
    /// <summary>
    /// The register that ships inside this assembly.
    /// </summary>
    internal const string EmbeddedResourceName = "Jellyfin.Plugin.MetadataSync.Fields.field-register.json";

    private static readonly Lazy<RegisterContents> _contents = new(() => Load(EmbeddedResourceName));

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>
    /// Gets every declared row, in the order the register writes them.
    /// </summary>
    public static IReadOnlyList<FieldRow> Rows => _contents.Value.Rows;

    /// <summary>
    /// Gets the kind groups the rows name, expanded.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> KindGroups => _contents.Value.KindGroups;

    /// <summary>
    /// Finds the row for a field.
    /// </summary>
    /// <param name="field">The field, named as the server names it.</param>
    /// <returns>The row, or null where the register declares no such field.</returns>
    public static FieldRow? Find(string field) =>
        Rows.FirstOrDefault(row => string.Equals(row.Field, field, StringComparison.Ordinal));

    /// <summary>
    /// Returns the row for a field that may move, and refuses everything else.
    /// </summary>
    /// <param name="field">The field, named as the server names it.</param>
    /// <returns>The row, which is guaranteed to be one that moves.</returns>
    /// <remarks>
    /// Nothing in the plugin calls this today. The one caller it had wrote a
    /// field outside a plan and is gone, and a pass asks <see cref="Find"/>
    /// instead, because the planner answers a field it may not move with a
    /// disposition on the row rather than by throwing: a pass that threw would
    /// stop at the first refused field on an item, and an operator would be
    /// handed one reason where the register has one per row. So this is the
    /// throwing face of the register, for a caller that wants one field or
    /// nothing, and it is reached from the suite and nowhere else.
    /// </remarks>
    /// <exception cref="FieldNotDeclaredException">
    /// The register declares no such field, or declares one that does not move.
    /// </exception>
    public static FieldRow RequireMovable(string field)
    {
        var row = Find(field);
        if (row is null)
        {
            throw new FieldNotDeclaredException(NoRowAtAll(field));
        }

        if (!row.Moves)
        {
            throw new FieldNotDeclaredException(ARowThatRefuses(row));
        }

        return row;
    }

    /// <summary>
    /// Reads a register out of this assembly by resource name.
    /// </summary>
    /// <param name="resourceName">The embedded resource to read.</param>
    /// <returns>The rows and the kind groups.</returns>
    internal static RegisterContents Load(string resourceName)
    {
        var assembly = typeof(FieldRegister).Assembly;
        // The refusal sits on the same line as the read on purpose. The suite
        // names a refusal site by the line of code that refuses, and a throw
        // continued onto its own line is reported at the line above it.
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException(NoRegisterEmbedded(resourceName));

        // Read the whole resource and hand the text to the one parser, so the
        // rows the plugin runs on and the rows a test hands in are built by the
        // same code. An embedded resource has a known length and is not a file,
        // so this is a read of the assembly rather than of a disk.
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return Parse(Encoding.UTF8.GetString(bytes));
    }

    /// <summary>
    /// Turns register text into rows, refusing text that does not describe one.
    /// </summary>
    /// <param name="text">The register, as JSON.</param>
    /// <returns>The rows and the kind groups.</returns>
    internal static RegisterContents Parse(string text)
    {
        var read = JsonSerializer.Deserialize<RegisterFile>(text, _json) ?? throw new InvalidOperationException(NothingToRead());

        var groups = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var pair in read.KindGroups)
        {
            groups[pair.Key] = new ReadOnlyCollection<string>(pair.Value);
        }

        var rows = new List<FieldRow>(read.Rows.Count);
        foreach (var row in read.Rows)
        {
            if (!groups.TryGetValue(row.Kinds, out var kinds))
            {
                throw new InvalidOperationException(NoSuchKindGroup(row));
            }

            rows.Add(new FieldRow(
                row.Field,
                row.DeclaredOn,
                row.ReachedBy,
                row.Kinds,
                kinds,
                row.Moves,
                Enum.Parse<FieldClass>(row.Class, ignoreCase: false),
                row.FromTheFile,
                GoverningLock(row),
                row.Reason,
                row.OperatorReason));
        }

        return new RegisterContents(new ReadOnlyCollection<FieldRow>(rows), groups);
    }

    /// <summary>
    /// Resolves the server lock a row names, refusing a name the server does
    /// not have. A lock nobody can set is a lock that refuses nothing, and it
    /// reads in the register exactly like one that works.
    /// </summary>
    private static MetadataField? GoverningLock(RegisterRow row)
    {
        if (row.Lock is null)
        {
            return null;
        }

        // Parsed by name and never by number, because Enum.TryParse reads "3"
        // as a member and a register saying 3 declares nothing a reader can
        // check.
        if (Array.IndexOf(Enum.GetNames<MetadataField>(), row.Lock) < 0)
        {
            throw new InvalidOperationException(NoSuchLock(row));
        }

        return Enum.Parse<MetadataField>(row.Lock, ignoreCase: false);
    }

    private static string NoRowAtAll(string field) => string.Format(
        CultureInfo.InvariantCulture,
        "The field register declares no field named '{0}', so it does not move. Add a row with its reason before anything writes it.",
        field);

    private static string ARowThatRefuses(FieldRow row) => string.Format(
        CultureInfo.InvariantCulture,
        "The field register declares '{0}' as a field that does not move: {1}",
        row.Field,
        row.Reason);

    private static string NoRegisterEmbedded(string resourceName) => string.Format(
        CultureInfo.InvariantCulture,
        "No field register is embedded under '{0}', so no field is declared and nothing may move.",
        resourceName);

    private static string NothingToRead() =>
        "The field register text describes no register, so no field is declared and nothing may move.";

    private static string NoSuchLock(RegisterRow row) => string.Format(
        CultureInfo.InvariantCulture,
        "The row for '{0}' names the lock '{1}', which is not a field the server lets an operator lock.",
        row.Field,
        row.Lock);

    private static string NoSuchKindGroup(RegisterRow row) => string.Format(
        CultureInfo.InvariantCulture,
        "The row for '{0}' names the kind group '{1}', which the register does not declare.",
        row.Field,
        row.Kinds);

    /// <summary>
    /// One reading of the register.
    /// </summary>
    internal sealed class RegisterContents
    {
        internal RegisterContents(IReadOnlyList<FieldRow> rows, IReadOnlyDictionary<string, IReadOnlyList<string>> kindGroups)
        {
            Rows = rows;
            KindGroups = kindGroups;
        }

        /// <summary>
        /// Gets the rows, in the order the register writes them.
        /// </summary>
        internal IReadOnlyList<FieldRow> Rows { get; }

        /// <summary>
        /// Gets the kind groups the rows name.
        /// </summary>
        internal IReadOnlyDictionary<string, IReadOnlyList<string>> KindGroups { get; }
    }

    private sealed class RegisterFile
    {
        [JsonPropertyName("kindGroups")]
        public Dictionary<string, List<string>> KindGroups { get; init; } = new(StringComparer.Ordinal);

        [JsonPropertyName("rows")]
        public List<RegisterRow> Rows { get; init; } = new();
    }

    private sealed class RegisterRow
    {
        [JsonPropertyName("field")]
        public string Field { get; init; } = string.Empty;

        [JsonPropertyName("declaredOn")]
        public string? DeclaredOn { get; init; }

        [JsonPropertyName("reachedBy")]
        public string? ReachedBy { get; init; }

        [JsonPropertyName("kinds")]
        public string Kinds { get; init; } = string.Empty;

        [JsonPropertyName("moves")]
        public bool Moves { get; init; }

        [JsonPropertyName("operatorReason")]
        public string OperatorReason { get; init; } = string.Empty;

        [JsonPropertyName("lock")]
        public string? Lock { get; init; }

        [JsonPropertyName("class")]
        public string Class { get; init; } = string.Empty;

        [JsonPropertyName("fromTheFile")]
        public bool FromTheFile { get; init; }

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = string.Empty;
    }
}
