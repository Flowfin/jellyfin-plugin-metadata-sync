using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Jellyfin.Plugin.MetadataSync.Configuration;

/// <summary>
/// Thrown when something asks for the configuration to act on and the
/// configuration cannot be acted on.
/// </summary>
/// <remarks>
/// It carries every problem rather than the first one. An operator repairing a
/// file by hand, told one thing at a time, saves once per repair and is refused
/// once per repair, and somewhere in that run they stop believing the plugin
/// knows what it wants.
/// <para>
/// The problems travel on the exception as well as inside the message, because
/// the two readers are different. The message is what a server log holds. The
/// list is what a page puts beside the control that caused each one, and a
/// renderer handed prose has to match strings to work out which control that
/// is.
/// </para>
/// <para>
/// One constructor, and the three a standard exception carries are absent
/// deliberately: a refusal with no reasons behind it is the state this type
/// exists to make impossible, and <c>jellyfin.ruleset</c> lowers the rule that
/// asks for them because constructors nothing calls are code nothing tests.
/// </para>
/// </remarks>
public sealed class ConfigurationRefusedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationRefusedException"/> class.
    /// </summary>
    /// <param name="problems">Every reason the configuration cannot be acted on.</param>
    public ConfigurationRefusedException(IReadOnlyList<ConfigurationProblem> problems)
        : base(Sentence(problems))
    {
        Problems = problems;
    }

    /// <summary>
    /// Gets every reason the configuration cannot be acted on, in the order the
    /// validator answered them.
    /// </summary>
    public IReadOnlyList<ConfigurationProblem> Problems { get; }

    private static string Sentence(IReadOnlyList<ConfigurationProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        return string.Format(
            CultureInfo.InvariantCulture,
            "This plugin's configuration cannot be acted on, for {0} reason(s): {1}",
            problems.Count,
            string.Join(" ", problems.Select(problem => problem.Message)));
    }
}
