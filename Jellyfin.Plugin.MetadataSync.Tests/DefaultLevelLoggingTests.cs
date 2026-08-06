using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the half of docs/personal-data.md that says a metadata field value
/// never appears in a log line at the default level.
///
/// The rule is expressed over the placeholder names in a message template
/// rather than over what an argument evaluates to. Structured logging makes
/// every value at a default-visible level arrive under a name the author chose,
/// CA1727 is already an error in jellyfin.ruleset so that name is PascalCase,
/// and a name is a thing a token scan can decide. What an expression returns is
/// not.
///
/// So the allowed names are a small set held below, everything else is refused,
/// and widening the set is a change a reviewer sees next to its reason. That is
/// deliberately the fail-closed direction: a new kind of thing in a log line
/// costs one line here, and a field value costs a red suite.
///
/// CA2254 already refuses an interpolated message template everywhere in this
/// repository. This guard covers the route CA2254 permits, which is a literal
/// template with a placeholder and the value passed beside it.
/// </summary>
public class DefaultLevelLoggingTests
{
    // The four levels a stock Jellyfin install writes to its log. Debug and
    // Trace are deliberately absent: an operator who turned those on to
    // diagnose one item asked for the detail on their own server, which is the
    // distinction docs/personal-data.md draws.
    private static readonly string[] DefaultVisibleCalls = new[]
    {
        "LogInformation",
        "LogWarning",
        "LogError",
        "LogCritical",
    };

    // What a log line at the default level is allowed to name. Every entry is a
    // count, an identifier, a name or an outcome, and none of them is the
    // content of a metadata field. The list lives here rather than in
    // docs/personal-data.md so that the document cannot become the authority
    // and drift away from what the suite actually refuses.
    //
    // The set is empty of anything a person typed. FieldName is here and
    // FieldValue is deliberately not, and those two are the near-miss the
    // fixtures below are built on, because they differ by the one word somebody
    // will get wrong.
    private static readonly string[] AllowedPlaceholders = new[]
    {
        "Attempt",
        "Bound",
        "Count",
        "DeferredCount",
        "Elapsed",
        "FieldCount",
        "FieldName",
        "ItemCount",
        "ItemId",
        "ItemKind",
        "LibraryId",
        "Outcome",
        "PairingId",
        "PassId",
        "Reason",
        "RefusedCount",
        "Version",
    };

    // An alignment and a format specifier end a placeholder's name.
    private static readonly char[] PlaceholderNameTerminators = new[] { ':', ',' };

    /// <summary>
    /// The sweep below reads files copied out of the plugin project by the test
    /// project file. If that copy stops happening the sweep passes over nothing
    /// and reads as a clean tree, so the set is asserted to be non-empty before
    /// anything is concluded from it.
    /// </summary>
    [Fact]
    public void ThePluginSourcesReachTheSweep()
    {
        Assert.NotEmpty(PluginSourceFiles());
    }

    /// <summary>
    /// The sweep. The plugin contains no logging call at all today, so this
    /// passes over an empty set and proves nothing about this tree; what proves
    /// the rule bites is the fixture pairs below. It is here so that the first
    /// logging call written into a reconciliation pass is judged by it rather
    /// than by whoever reviews that change.
    /// </summary>
    [Fact]
    public void NoDefaultLevelLoggingCallInThePluginNamesAFieldValue()
    {
        var refused = new List<string>();

        foreach (var file in PluginSourceFiles())
        {
            foreach (var reason in ValueCarryingCalls(File.ReadAllText(file)))
            {
                refused.Add(Path.GetFileName(file) + ": " + reason);
            }
        }

        Assert.True(
            refused.Count == 0,
            "A logging call at a default-visible level can carry a metadata field value: "
                + string.Join("; ", refused)
                + ". docs/personal-data.md says the default level carries the shape of what happened and never the content.");
    }

    /// <summary>
    /// The bite, on the pair that differ by one word. A field's name says which
    /// field was involved and is exactly what an operator reading a log needs.
    /// </summary>
    [Fact]
    public void APlaceholderNamingAFieldIsNotRefused()
    {
        Assert.Empty(ValueCarryingCalls(
            "_logger.LogInformation(\"Refused {FieldName}\", field.Name);"));
    }

    /// <summary>
    /// The neighbour, differing by one word. The same line naming the value
    /// instead of the field is the leak the rule exists for, and it is the one
    /// CA2254 lets through because the template is still a constant.
    /// </summary>
    [Fact]
    public void APlaceholderNamingAFieldValueIsRefused()
    {
        Assert.NotEmpty(ValueCarryingCalls(
            "_logger.LogInformation(\"Refused {FieldValue}\", field.Value);"));
    }

    /// <summary>
    /// A count is what the default level is mostly for, and a guard that
    /// refused this would push every useful line down to debug and then be
    /// turned off by the next person who needed one.
    /// </summary>
    [Fact]
    public void ACountIsNotRefused()
    {
        Assert.Empty(ValueCarryingCalls(
            "_logger.LogInformation(\"Wrote {FieldCount} fields\", written.Count);"));
    }

    /// <summary>
    /// An item's overview under its own name is the plainest form of the leak,
    /// and the field it names is free text, which docs/personal-data.md treats
    /// as capable of carrying personal data.
    /// </summary>
    [Fact]
    public void APlaceholderNamingAFreeTextFieldIsRefused()
    {
        Assert.NotEmpty(ValueCarryingCalls(
            "_logger.LogInformation(\"Wrote overview {Overview}\", item.Overview);"));
    }

    /// <summary>
    /// A value passed with nothing naming it still reaches the log event, so an
    /// argument the template does not account for is refused. Without this the
    /// way past the rule is to delete a placeholder.
    /// </summary>
    [Fact]
    public void AnArgumentTheTemplateDoesNotNameIsRefused()
    {
        Assert.NotEmpty(ValueCarryingCalls(
            "_logger.LogInformation(\"The pass completed.\", item.Overview);"));
    }

    /// <summary>
    /// A template on its own is the commonest line in the plugin and is the
    /// shape the rule leaves entirely alone.
    /// </summary>
    [Fact]
    public void ATemplateWithNoPlaceholderAndNoArgumentIsNotRefused()
    {
        Assert.Empty(ValueCarryingCalls(
            "_logger.LogInformation(\"The pass completed.\");"));
    }

    /// <summary>
    /// An exception handed to the call before its template is allowed, and
    /// docs/personal-data.md states that as an open route rather than a covered
    /// one: an exception thrown by a library call can carry in its own message
    /// the value that caused it.
    /// </summary>
    [Fact]
    public void AnExceptionBeforeTheTemplateIsNotRefused()
    {
        Assert.Empty(ValueCarryingCalls(
            "_logger.LogError(ex, \"The pass stopped.\");"));
    }

    /// <summary>
    /// An interpolated template is refused here as well as by CA2254. The two
    /// overlap deliberately: a ruleset entry is edited in one line, and a rule
    /// only one thing holds up has one place to fail.
    /// </summary>
    [Fact]
    public void AnInterpolatedTemplateIsRefused()
    {
        Assert.NotEmpty(ValueCarryingCalls(
            "_logger.LogWarning($\"Refused {field.Value}\");"));
    }

    /// <summary>
    /// A template the scan cannot read is refused rather than passed. A guard
    /// over source text judges spelling, so the case it cannot see has to fail
    /// closed or the way around it is one local variable.
    /// </summary>
    [Fact]
    public void ATemplateHeldInAVariableIsRefusedAsUnjudgeable()
    {
        Assert.NotEmpty(ValueCarryingCalls(
            "_logger.LogInformation(message);"));
    }

    /// <summary>
    /// A logging call written inside a comment or a string is not a logging
    /// call. Without this the guard refuses the documentation of itself, which
    /// is a failure this family of checks has produced before.
    /// </summary>
    [Fact]
    public void ACallInsideACommentOrAStringIsNotRefused()
    {
        Assert.Empty(ValueCarryingCalls(
            "// _logger.LogInformation(\"x {Overview}\", v);\n"
                + "var sample = \"_logger.LogInformation(\\\"x {Overview}\\\", v)\";\n"
                + "/* _logger.LogError(\"x {Overview}\", v); */"));
    }

    /// <summary>
    /// A placeholder may carry an alignment and a format specifier, and neither
    /// is part of the name. A guard that read them as part of it would refuse
    /// an allowed name for a reason nobody could see.
    /// </summary>
    [Fact]
    public void AFormatSpecifierIsNotPartOfThePlaceholderName()
    {
        Assert.Empty(ValueCarryingCalls(
            "_logger.LogInformation(\"Took {Elapsed:N0} ms over {Count,6} items\", ms, n);"));
    }

    // The plugin's own sources, copied beside the test binary by the test
    // project file. Reading them from a path relative to the source tree would
    // work on a developer's machine and not in a packaging job.
    private static IReadOnlyList<string> PluginSourceFiles()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "plugin-sources");

        if (!Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    // Returns one description per default-visible logging call that could carry
    // a metadata field value, and an empty list for source that could not. It
    // reads tokens rather than a syntax tree, which is the bound
    // docs/personal-data.md states: it judges the spelling of a call and never
    // what a call means.
    private static IReadOnlyList<string> ValueCarryingCalls(string source)
    {
        var refused = new List<string>();
        var i = 0;

        while (i < source.Length)
        {
            var c = source[i];

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                var newline = source.IndexOf('\n', i);
                i = newline < 0 ? source.Length : newline + 1;
                continue;
            }

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                var close = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? source.Length : close + 2;
                continue;
            }

            if (c == '"' || c == '\'' || c == '@' || c == '$')
            {
                var after = EndOfLiteral(source, i, out _);
                if (after > i)
                {
                    i = after;
                    continue;
                }
            }

            if (IsIdentifierPart(c))
            {
                var end = i;
                while (end < source.Length && IsIdentifierPart(source[end]))
                {
                    end++;
                }

                var word = source[i..end];
                var precededByIdentifier = i > 0 && IsIdentifierPart(source[i - 1]);
                var open = SkipWhitespace(source, end);

                if (!precededByIdentifier
                    && DefaultVisibleCalls.Contains(word, StringComparer.Ordinal)
                    && open < source.Length
                    && source[open] == '(')
                {
                    var reason = JudgeCall(source, open, out var afterCall);
                    if (reason is not null)
                    {
                        refused.Add(word + ": " + reason);
                    }

                    i = afterCall;
                    continue;
                }

                i = end;
                continue;
            }

            i++;
        }

        return refused;
    }

    // Judges one call, given the index of its opening parenthesis. Returns null
    // when the call cannot carry a field value, and the reason it can otherwise.
    private static string? JudgeCall(string source, int open, out int afterCall)
    {
        var arguments = SplitArguments(source, open, out afterCall, out var sawInterpolation, out var closed);

        if (!closed)
        {
            return "the argument list is not closed in this file, so the call cannot be judged";
        }

        if (sawInterpolation)
        {
            return "an interpolated string reaches the call, which puts the value inside the message text";
        }

        var template = arguments.ToList().FindIndex(IsStringLiteral);

        if (template < 0)
        {
            return "no literal message template, so what the call writes cannot be read here";
        }

        if (template > 1)
        {
            return "more than one argument before the message template";
        }

        var placeholders = Placeholders(arguments[template]);
        var outside = placeholders.Where(p => !AllowedPlaceholders.Contains(p, StringComparer.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (outside.Count > 0)
        {
            return "the template names " + string.Join(", ", outside)
                + ", which is outside the set a default-level line may carry";
        }

        var passed = arguments.Count - template - 1;

        if (passed != placeholders.Count)
        {
            return passed.ToString(CultureInfo.InvariantCulture) + " argument(s) for "
                + placeholders.Count.ToString(CultureInfo.InvariantCulture)
                + " placeholder(s), so a value reaches the log event under no name at all";
        }

        return null;
    }

    // Splits the top-level arguments of a call whose opening parenthesis is at
    // open. Literals and comments are stepped over whole, so a parenthesis or a
    // comma inside one does not split anything.
    private static IReadOnlyList<string> SplitArguments(
        string source,
        int open,
        out int afterCall,
        out bool sawInterpolation,
        out bool closed)
    {
        var arguments = new List<string>();
        var depth = 1;
        var start = open + 1;
        var i = start;

        sawInterpolation = false;

        while (i < source.Length && depth > 0)
        {
            var c = source[i];

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                var newline = source.IndexOf('\n', i);
                i = newline < 0 ? source.Length : newline + 1;
                continue;
            }

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                var close = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? source.Length : close + 2;
                continue;
            }

            if (c == '"' || c == '\'' || c == '@' || c == '$')
            {
                var after = EndOfLiteral(source, i, out var isInterpolated);
                if (after > i)
                {
                    sawInterpolation |= isInterpolated;
                    i = after;
                    continue;
                }
            }

            if (c == '(' || c == '[' || c == '{')
            {
                depth++;
                i++;
                continue;
            }

            if (c == ')' || c == ']' || c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }

                i++;
                continue;
            }

            if (c == ',' && depth == 1)
            {
                arguments.Add(source[start..i].Trim());
                start = i + 1;
                i++;
                continue;
            }

            i++;
        }

        closed = depth == 0;
        afterCall = closed ? i + 1 : source.Length;

        if (!closed)
        {
            return arguments;
        }

        var tail = source[start..i].Trim();

        if (tail.Length > 0 || arguments.Count > 0)
        {
            arguments.Add(tail);
        }

        return arguments;
    }

    // The names a message template hands values to. A doubled brace is an
    // escaped brace and names nothing, and an alignment or a format specifier
    // is not part of the name.
    private static IReadOnlyList<string> Placeholders(string template)
    {
        var names = new List<string>();
        var i = 0;

        while (i < template.Length)
        {
            if (template[i] != '{')
            {
                i++;
                continue;
            }

            if (i + 1 < template.Length && template[i + 1] == '{')
            {
                i += 2;
                continue;
            }

            var close = template.IndexOf('}', i + 1);

            if (close < 0)
            {
                break;
            }

            var inner = template[(i + 1)..close];
            var cut = inner.IndexOfAny(PlaceholderNameTerminators);

            names.Add((cut < 0 ? inner : inner[..cut]).Trim());
            i = close + 1;
        }

        return names;
    }

    private static bool IsStringLiteral(string argument)
        => argument.StartsWith('"') || argument.StartsWith("@\"", StringComparison.Ordinal);

    private static bool IsIdentifierPart(char c)
        => char.IsLetterOrDigit(c) || c == '_';

    private static int SkipWhitespace(string source, int from)
    {
        var i = from;

        while (i < source.Length && char.IsWhiteSpace(source[i]))
        {
            i++;
        }

        return i;
    }

    // Returns the index just past a literal beginning at start, or start itself
    // when there is no literal there. The verbatim, interpolated and raw forms
    // are all recognised, because a scan that stops understanding a literal
    // starts reading its contents as code.
    private static int EndOfLiteral(string source, int start, out bool isInterpolated)
    {
        var i = start;
        var verbatim = false;

        isInterpolated = false;

        while (i < source.Length && (source[i] == '@' || source[i] == '$'))
        {
            verbatim |= source[i] == '@';
            isInterpolated |= source[i] == '$';
            i++;
        }

        if (i >= source.Length || (source[i] != '"' && source[i] != '\''))
        {
            isInterpolated = false;
            return start;
        }

        if (source[i] == '\'')
        {
            i++;

            while (i < source.Length && source[i] != '\'')
            {
                i += source[i] == '\\' ? 2 : 1;
            }

            return Math.Min(i + 1, source.Length);
        }

        if (i + 2 < source.Length && source[i + 1] == '"' && source[i + 2] == '"')
        {
            return EndOfRawString(source, i);
        }

        i++;

        while (i < source.Length)
        {
            var c = source[i];

            if (verbatim)
            {
                if (c != '"')
                {
                    i++;
                    continue;
                }

                if (i + 1 < source.Length && source[i + 1] == '"')
                {
                    i += 2;
                    continue;
                }

                return i + 1;
            }

            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == '"' || c == '\n')
            {
                return i + 1;
            }

            i++;
        }

        return i;
    }

    private static int EndOfRawString(string source, int start)
    {
        var i = start;
        var fence = 0;

        while (i < source.Length && source[i] == '"')
        {
            fence++;
            i++;
        }

        while (i < source.Length)
        {
            if (source[i] != '"')
            {
                i++;
                continue;
            }

            var run = i;

            while (run < source.Length && source[run] == '"')
            {
                run++;
            }

            if (run - i >= fence)
            {
                return run;
            }

            i = run;
        }

        return i;
    }
}
