using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Jellyfin.Plugin.MetadataSync.Tests;

/// <summary>
/// Holds the endpoints this plugin adds against the table that decides who may
/// call them.
/// </summary>
/// <remarks>
/// Every endpoint here is an endpoint on somebody's media server, reachable by
/// anything that can reach the server, and everything this plugin will expose is
/// an administrator action. The authorization is decided in
/// <c>docs/endpoints.md</c> before the endpoints exist, and this reads that
/// document rather than restating it, so the table and the code cannot drift
/// apart quietly.
///
/// A reflection walk rather than a review habit, because the failure is an
/// endpoint added later with the attribute forgotten, and review catches the
/// endpoint somebody wrote and misses the one somebody copied. It fails in both
/// directions: an action with no row, and a row naming no action.
///
/// It passes today because the plugin declares no controller, which is the
/// honest reason and no proof of anything. The table is installed while the
/// answer is trivially yes so that the first endpoint cannot arrive without
/// somebody writing down who may call it.
///
/// What it cannot catch, stated rather than left to be assumed. It answers what
/// the attributes say and never what the server does with them: a policy name
/// the server does not have passes here and refuses every caller at run time,
/// and a policy that grants more than its name suggests is not something any
/// reading of this tree judges. An action routed by convention rather than by an
/// attribute is reported with no method, which fails closed rather than being
/// skipped. And it says nothing about what an endpoint does once a caller is
/// past the policy, which is the half of #54 that needs endpoints to exist.
/// </remarks>
public class EndpointAuthorizationTests
{
    /// <summary>
    /// What the walk reports where an action carries no policy at all, and what
    /// a row may never carry. Written once because the two ends are the same
    /// rule: an endpoint reaches this state by having its attribute removed, or
    /// by never having had one.
    /// </summary>
    private const string NoPolicy = "(none)";

    /// <summary>
    /// What the walk reports where an action names no HTTP method. Routing by
    /// convention is not used here, and an action that arrived without an
    /// attribute is reported rather than skipped.
    /// </summary>
    private const string NoMethod = "(unstated)";

    /// <summary>
    /// The rule. Every endpoint the plugin adds is in the table, with the policy
    /// the table gives it.
    /// </summary>
    [Fact]
    public void EveryEndpointThisPluginAddsIsInTheTableWithItsPolicy()
    {
        var walked = EndpointsDeclaredBy(typeof(Plugin).Assembly);
        var tabled = EndpointsInTheTable();

        Assert.Empty(walked.Except(tabled, StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The rule the other way round. A row for an endpoint that no longer exists
    /// is a row somebody reads and trusts, and it is the state a deleted
    /// controller leaves behind.
    /// </summary>
    [Fact]
    public void EveryRowInTheTableNamesAnEndpointThisPluginAdds()
    {
        var walked = EndpointsDeclaredBy(typeof(Plugin).Assembly);
        var tabled = EndpointsInTheTable();

        Assert.Empty(tabled.Except(walked, StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// No endpoint this plugin adds is reachable without authenticating, and no
    /// row in the table says one is.
    /// </summary>
    [Fact]
    public void NothingIsReachableWithoutAPolicy()
    {
        var open = EndpointsDeclaredBy(typeof(Plugin).Assembly)
            .Concat(EndpointsInTheTable())
            .Where(entry => entry.EndsWith(" " + NoPolicy, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(open);
    }

    /// <summary>
    /// The walk reads a document. If it ever reads an empty one, or one whose
    /// table it cannot find, both rules above pass for the wrong reason, so what
    /// was read is asserted before anything is concluded from a clean run.
    /// </summary>
    [Fact]
    public void TheTableIsTheDocumentAndTheDocumentWasRead()
    {
        var document = TheTableDocument();

        Assert.NotEmpty(document);
        Assert.Contains("| Method | Route | Policy | What it exposes |", document, StringComparison.Ordinal);
    }

    /// <summary>
    /// The bite, executed rather than argued. This suite's own assembly declares
    /// a controller with an action nobody put a policy on, so the same walk run
    /// over it has to report that action as reachable without one. A walk that
    /// found nothing anywhere would pass the rules above on any tree at all.
    /// </summary>
    [Fact]
    public void TheWalkFindsAnActionThatCarriesNoPolicy()
    {
        var walked = EndpointsDeclaredBy(typeof(EndpointAuthorizationTests).Assembly);

        Assert.Contains("GET Fixture/Open/Everything " + NoPolicy, walked, StringComparer.Ordinal);
    }

    /// <summary>
    /// The neighbour, and it is why the walk reads the controller as well as the
    /// action. A policy declared on the controller covers every action on it,
    /// which is the shape this plugin's endpoints are meant to use. A rule that
    /// only read the action would report this one as unprotected and send back
    /// the correct code.
    /// </summary>
    [Fact]
    public void TheWalkReadsAPolicyDeclaredOnTheController()
    {
        var walked = EndpointsDeclaredBy(typeof(EndpointAuthorizationTests).Assembly);

        Assert.Contains(
            "POST Fixture/Elevated/Start " + Policies.RequiresElevation,
            walked,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The policy the table names is one the server actually has. A table whose
    /// vocabulary has drifted from the server names a policy no caller can ever
    /// satisfy, and it reads exactly like a table that is correct.
    /// </summary>
    [Fact]
    public void ThePolicyTheTableNamesIsOneTheServerActuallyHas()
    {
        var policies = typeof(Policies)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string?)field.GetRawConstantValue())
            .ToList();

        Assert.Contains(Policies.RequiresElevation, policies, StringComparer.Ordinal);

        var named = EndpointsInTheTable()
            .Select(entry => entry[(entry.LastIndexOf(' ') + 1)..])
            .Distinct(StringComparer.Ordinal)
            .Where(policy => !policies.Contains(policy, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(named);
    }

    /// <summary>
    /// Walks one assembly for controllers and returns one entry per endpoint, as
    /// its HTTP method, its route and the policy in force, sorted so a failure
    /// reads the same way twice.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The endpoints that assembly declares.</returns>
    private static IReadOnlyList<string> EndpointsDeclaredBy(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var entries = new List<string>();

        foreach (var controller in assembly.GetTypes().Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract))
        {
            var policy = PolicyOn(controller);

            foreach (var action in ActionsOn(controller))
            {
                foreach (var method in MethodsOf(action))
                {
                    entries.Add(method + " " + RouteOf(controller, action) + " " + (PolicyOn(action) ?? policy ?? NoPolicy));
                }
            }
        }

        return entries.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Reads the table out of the document and returns it in the same shape the
    /// walk produces, so the two are compared rather than described.
    /// </summary>
    /// <returns>The endpoints the table declares.</returns>
    private static IReadOnlyList<string> EndpointsInTheTable()
    {
        var entries = new List<string>();

        foreach (var line in TheTableDocument().Split('\n').Select(raw => raw.TrimEnd('\r').Trim()))
        {
            if (!line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim('|').Split('|').Select(cell => cell.Trim().Trim('`').Trim()).ToList();
            if (cells.Count != 4 || string.Equals(cells[0], "Method", StringComparison.Ordinal) || cells[0].StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            entries.Add(cells[0] + " " + cells[1] + " " + cells[2]);
        }

        return entries.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The action methods on a controller. Public and declared on the type
    /// itself, because the base class the server requires carries members that
    /// are not endpoints, and a member the author marked as not an action is not
    /// one.
    /// </summary>
    /// <param name="controller">The controller type.</param>
    /// <returns>Its action methods.</returns>
    private static IEnumerable<MethodInfo> ActionsOn(Type controller)
    {
        return controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Where(method => method.GetCustomAttribute<NonActionAttribute>(inherit: true) is null);
    }

    /// <summary>
    /// The HTTP methods an action answers. An action with no attribute is
    /// reported with a placeholder rather than skipped, so routing by convention
    /// fails closed instead of leaving an endpoint out of the comparison.
    /// </summary>
    /// <param name="action">The action method.</param>
    /// <returns>Its HTTP methods, upper case.</returns>
    private static IEnumerable<string> MethodsOf(MethodInfo action)
    {
        var declared = action
            .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
            .SelectMany(attribute => attribute.HttpMethods)
            .Select(method => method.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return declared.Count == 0 ? new[] { NoMethod } : declared;
    }

    /// <summary>
    /// The route an action answers on, taken from the controller's template and
    /// the action's together rather than written down twice.
    /// </summary>
    /// <param name="controller">The controller type.</param>
    /// <param name="action">The action method.</param>
    /// <returns>The route.</returns>
    private static string RouteOf(Type controller, MethodInfo action)
    {
        var prefix = controller
            .GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(attribute => attribute.Template)
            .FirstOrDefault(template => !string.IsNullOrEmpty(template)) ?? string.Empty;

        var suffix = action
            .GetCustomAttributes<HttpMethodAttribute>(inherit: true)
            .Select(attribute => attribute.Template)
            .FirstOrDefault(template => !string.IsNullOrEmpty(template)) ?? string.Empty;

        // A template rooted at the site replaces the controller's prefix rather
        // than hanging off it, which is what the two leading spellings mean.
        var route = suffix.StartsWith('/') || suffix.StartsWith("~/", StringComparison.Ordinal)
            ? suffix.TrimStart('~')
            : string.Join('/', new[] { prefix, suffix }.Where(part => part.Length > 0));

        return route
            .Replace("[controller]", ControllerToken(controller), StringComparison.Ordinal)
            .Trim('/');
    }

    /// <summary>
    /// What the routing token for a controller expands to, which is its type
    /// name without the suffix the framework strips.
    /// </summary>
    /// <param name="controller">The controller type.</param>
    /// <returns>The token's value.</returns>
    private static string ControllerToken(Type controller)
    {
        const string Suffix = "Controller";

        return controller.Name.EndsWith(Suffix, StringComparison.Ordinal)
            ? controller.Name[..^Suffix.Length]
            : controller.Name;
    }

    /// <summary>
    /// The policy in force on a controller or an action, or null where the
    /// member says nothing and the answer is the other one's.
    /// </summary>
    /// <param name="member">The controller type or the action method.</param>
    /// <returns>The policy name, the placeholder where the member opens the door, or null.</returns>
    private static string? PolicyOn(MemberInfo member)
    {
        if (member.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null)
        {
            return NoPolicy;
        }

        var authorize = member.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();
        if (authorize.Count == 0)
        {
            return null;
        }

        return authorize
            .Select(attribute => attribute.Policy)
            .FirstOrDefault(policy => !string.IsNullOrEmpty(policy)) ?? NoPolicy;
    }

    /// <summary>
    /// Reads the table document out of the test output, where the project file
    /// copies it. Walking up from the test binary to the repository would answer
    /// a different question on a machine where the suite runs from somewhere
    /// else.
    /// </summary>
    /// <returns>The document's text.</returns>
    private static string TheTableDocument()
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "endpoints.md"), System.Text.Encoding.UTF8);
    }
}
