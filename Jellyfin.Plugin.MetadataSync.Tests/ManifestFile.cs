using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Reads the packaging manifests, which are what tells a server which line a
/// package is for and which version it carries.
/// </summary>
/// <remarks>
/// There is one manifest per supported server line and the set is read from the
/// files beside the test binary rather than from a list written here. The test
/// project copies every <c>build*.yaml</c> at the repository root, so a manifest
/// added for a third line is judged by every leg that walks this set instead of
/// being invisible until somebody remembers to add its name.
/// <para>
/// It is one reader rather than a copy in each suite that needs one, for the
/// reason <see cref="PluginProjectFile"/> gives: two readings of one file drift,
/// and the copy a failing test happens to use is then the one that is wrong.
/// </para>
/// </remarks>
internal static class ManifestFile
{
    /// <summary>
    /// The manifest the packaging tool reads when nothing has been staged over
    /// it, which is the 10.11 line's.
    /// </summary>
    public const string Default = "build.yaml";

    /// <summary>
    /// Gets the manifests beside the test binary, in ordinal order of their
    /// names.
    /// </summary>
    /// <returns>The file names, without a directory.</returns>
    public static IReadOnlyList<string> Names()
    {
        var found = Directory.GetFiles(AppContext.BaseDirectory, "build*.yaml")
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Contains(Default, found, StringComparer.Ordinal);
        return found;
    }

    /// <summary>
    /// Reads the text of one manifest.
    /// </summary>
    /// <param name="name">The file name, without a directory.</param>
    /// <returns>The manifest text.</returns>
    public static string Text(string name)
    {
        var path = Path.Join(AppContext.BaseDirectory, name);

        Assert.True(File.Exists(path), name + " is not beside the test binary. The test project copies every build*.yaml at the repository root.");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Reads one quoted scalar field out of a manifest, and fails the test with
    /// the reason if it cannot be read.
    /// </summary>
    /// <param name="name">The manifest file name.</param>
    /// <param name="field">The field to read.</param>
    /// <returns>The value.</returns>
    public static string Field(string name, string field)
    {
        var read = FieldIn(Text(name), field);

        Assert.True(read.Failure is null, name + ": " + read.Failure);
        return read.Value!;
    }

    /// <summary>
    /// Reads one block scalar out of a manifest, and fails the test with the
    /// reason if it cannot be read.
    /// </summary>
    /// <param name="name">The manifest file name.</param>
    /// <param name="field">The field to read.</param>
    /// <returns>The block's lines, trimmed and with the blank ones dropped.</returns>
    public static IReadOnlyList<string> Block(string name, string field)
    {
        var read = BlockIn(Text(name), field);

        Assert.True(read.Failure is null, name + ": " + read.Failure);
        return read.Value!;
    }

    /// <summary>
    /// Reads one quoted scalar field out of the manifest text.
    /// </summary>
    /// <remarks>
    /// The text is split into lines rather than matched whole under
    /// <see cref="RegexOptions.Multiline"/>, because in .NET a multiline
    /// <c>$</c> matches the position before a <c>\n</c> and leaves a <c>\r</c>
    /// sitting in front of it, so a trailing anchor never matches on a CRLF
    /// file. Splitting makes the carriage return part of the line, where it is
    /// trimmed once and by name.
    /// </remarks>
    /// <param name="manifest">The manifest text, with either line ending.</param>
    /// <param name="name">The field to read.</param>
    /// <returns>The value, or the reason it could not be read.</returns>
    public static (string? Value, string? Failure) FieldIn(string manifest, string name)
    {
        var lines = Lines(manifest);
        var field = new Regex("^" + Regex.Escape(name) + ":[ \t]*\"([^\"]*)\"[ \t]*$");

        var match = lines.Select(l => field.Match(l)).FirstOrDefault(m => m.Success);
        if (match is not null)
        {
            return (match.Groups[1].Value, null);
        }

        // A text carrying no key line at all is not a manifest missing one
        // field. It is a file that was not read, was not the manifest, or was
        // not parsed, and saying so is what keeps the next reader from
        // editing the manifest to add a field that is already there.
        if (!lines.Any(IsAKeyLine))
        {
            return (null, "the manifest parsed as no field at all, so it was not read as one. Looking for '" + name + "'.");
        }

        return (null, "the manifest declares no quoted '" + name + "' field.");
    }

    /// <summary>
    /// Reads one block scalar out of the manifest text.
    /// </summary>
    /// <remarks>
    /// The prose fields an operator reads are folded blocks rather than quoted
    /// scalars, so <see cref="FieldIn"/> cannot see them, and they are exactly
    /// the fields that must stay identical between one plugin's manifests. The
    /// block is the indented lines under the key: the indentation is what YAML
    /// itself uses to end one, and a key line at the outer level is where this
    /// stops.
    /// </remarks>
    /// <param name="manifest">The manifest text, with either line ending.</param>
    /// <param name="name">The field to read.</param>
    /// <returns>The block's non-blank lines, trimmed, or the reason it could not be read.</returns>
    public static (IReadOnlyList<string>? Value, string? Failure) BlockIn(string manifest, string name)
    {
        var lines = Lines(manifest);
        var opener = new Regex("^" + Regex.Escape(name) + ":[ \t]*[>|][-+]?[ \t]*$");

        var start = lines.FindIndex(l => opener.IsMatch(l));
        if (start < 0)
        {
            if (!lines.Any(IsAKeyLine))
            {
                return (null, "the manifest parsed as no field at all, so it was not read as one. Looking for the '" + name + "' block.");
            }

            return (null, "the manifest declares no '" + name + "' block.");
        }

        var body = new List<string>();
        for (var i = start + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                break;
            }

            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                body.Add(trimmed);
            }
        }

        if (body.Count == 0)
        {
            return (null, "the manifest's '" + name + "' block is empty.");
        }

        return (body, null);
    }

    /// <summary>
    /// Whether a line opens a field at the outer level of the document.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>Whether it is a key line.</returns>
    private static bool IsAKeyLine(string line) => Regex.IsMatch(line, "^[A-Za-z][A-Za-z0-9_-]*:");

    /// <summary>
    /// Splits a manifest into lines with either line ending.
    /// </summary>
    /// <param name="manifest">The manifest text.</param>
    /// <returns>The lines.</returns>
    private static List<string> Lines(string manifest)
        => manifest.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
}
