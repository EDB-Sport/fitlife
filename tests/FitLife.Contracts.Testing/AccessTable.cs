using System.Text.RegularExpressions;
using FitLife.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FitLife.Contracts.Testing;

/// <summary>
/// Compares a service's rows in <c>docs/domain/ACCESS.md</c> with the service's real endpoints (#489):
/// route, HTTP method, Authorize roles and policies, and AllowAnonymous, read from the endpoint metadata in-process.
/// Rows marked "(planned)" and rows without an HTTP method (pages, <c>/mcp</c>) are skipped. <c>/health</c>, the
/// OpenAPI document and Scalar have their own shared rows, so their endpoints are skipped too.
/// </summary>
public static partial class AccessTable
{
    private const string Yes = "yes";
    private const string No = "–";
    private static readonly string[] Roles = [FitLifeRoles.Medlem, FitLifeRoles.Traener, FitLifeRoles.Admin];
    private static readonly string[] SharedRowPaths = ["/health", "/openapi", "/scalar"];

    /// <summary>Compares the service's rows in the repo's <c>docs/domain/ACCESS.md</c> with the app's endpoints.</summary>
    /// <param name="service">The Service column of the rows, e.g. <c>BookingService</c>.</param>
    /// <param name="services">The app's services, which hold its endpoints and authorization policies.</param>
    /// <returns>One line per difference; empty when the rows match the endpoints.</returns>
    public static async Task<IReadOnlyList<string>> CompareAsync(string service, IServiceProvider services)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "docs", "domain", "ACCESS.md");
        string accessMarkdown = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        return await CompareAsync(service, accessMarkdown, services).ConfigureAwait(false);
    }

    /// <summary>Compares the service's rows in <paramref name="accessMarkdown"/> with the app's endpoints.</summary>
    /// <param name="service">The Service column of the rows, e.g. <c>BookingService</c>.</param>
    /// <param name="accessMarkdown">The text of <c>docs/domain/ACCESS.md</c>.</param>
    /// <param name="services">The app's services, which hold its endpoints and authorization policies.</param>
    /// <returns>One line per difference; empty when the rows match the endpoints.</returns>
    public static Task<IReadOnlyList<string>> CompareAsync(string service, string accessMarkdown, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return CompareAsync(
            service,
            accessMarkdown,
            services.GetRequiredService<EndpointDataSource>().Endpoints,
            services.GetRequiredService<IAuthorizationPolicyProvider>());
    }

    /// <summary>Compares the service's rows in <paramref name="accessMarkdown"/> with <paramref name="endpoints"/>.</summary>
    /// <param name="service">The Service column of the rows, e.g. <c>BookingService</c>.</param>
    /// <param name="accessMarkdown">The text of <c>docs/domain/ACCESS.md</c>.</param>
    /// <param name="endpoints">The app's endpoints.</param>
    /// <param name="policies">The app's authorization policies, which give the roles behind a policy name.</param>
    /// <returns>One line per difference; empty when the rows match the endpoints.</returns>
    public static async Task<IReadOnlyList<string>> CompareAsync(
        string service, string accessMarkdown, IEnumerable<Endpoint> endpoints, IAuthorizationPolicyProvider policies)
    {
        ArgumentNullException.ThrowIfNull(accessMarkdown);
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(policies);

        List<Row> rows = [.. ReadRows(accessMarkdown).Where(row => string.Equals(row.Service, service, StringComparison.Ordinal))];
        Dictionary<string, string> actualEndpoints = await ReadEndpointsAsync(endpoints, policies).ConfigureAwait(false);
        List<string> differences = [];

        foreach (Row row in rows.Where(row => !row.Planned))
        {
            if (!actualEndpoints.TryGetValue(row.Endpoint, out string? actual))
            {
                differences.Add($"{row.Endpoint}: ACCESS.md has a row, but {service} has no such endpoint. Mark the row (planned) or remove it");
            }
            else if (!string.Equals(row.Cells, actual, StringComparison.Ordinal))
            {
                differences.Add($"{row.Endpoint}: ACCESS.md says Open | Medlem | Træner | Admin = {row.Cells}, the endpoint metadata says {actual}");
            }
        }

        HashSet<string> builtRows = [.. rows.Where(row => !row.Planned).Select(row => row.Endpoint)];
        HashSet<string> plannedRows = [.. rows.Where(row => row.Planned).Select(row => row.Endpoint)];
        foreach (string endpoint in actualEndpoints.Keys.Where(endpoint => !builtRows.Contains(endpoint)).Order(StringComparer.Ordinal))
        {
            differences.Add(plannedRows.Contains(endpoint)
                ? $"{endpoint}: the endpoint exists, but its row in ACCESS.md is still marked (planned)"
                : $"{endpoint}: {service} has this endpoint, but ACCESS.md has no row for it");
        }

        return differences;
    }

    private static IEnumerable<Row> ReadRows(string accessMarkdown)
    {
        foreach (string line in accessMarkdown.Split('\n'))
        {
            string[] cells = [.. line.Trim().Trim('|').Split('|').Select(cell => cell.Trim())];
            if (cells.Length < 6)
            {
                continue;
            }

            Match endpoint = EndpointCell().Match(cells[0]);
            if (!endpoint.Success)
            {
                continue;
            }

            yield return new Row(
                $"{endpoint.Groups["method"].Value} {NormalisePath(endpoint.Groups["path"].Value)}",
                cells[1],
                endpoint.Groups["rest"].Value.Contains("(planned)", StringComparison.Ordinal),
                string.Join(" | ", cells[2..6]));
        }
    }

    private static async Task<Dictionary<string, string>> ReadEndpointsAsync(IEnumerable<Endpoint> endpoints, IAuthorizationPolicyProvider policies)
    {
        Dictionary<string, string> actualEndpoints = new(StringComparer.Ordinal);

        foreach (RouteEndpoint endpoint in endpoints.OfType<RouteEndpoint>())
        {
            IHttpMethodMetadata? methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();
            string path = NormalisePath(endpoint.RoutePattern.RawText ?? string.Empty);
            if (methods is null || SharedRowPaths.Any(shared => path.StartsWith(shared, StringComparison.Ordinal)))
            {
                continue;
            }

            string cells = await CellsAsync(endpoint, policies).ConfigureAwait(false);
            foreach (string method in methods.HttpMethods)
            {
                actualEndpoints[$"{method} {path}"] = cells;
            }
        }

        return actualEndpoints;
    }

    /// <summary>The Open, Medlem, Træner and Admin cells the endpoint's metadata gives, as ACCESS.md writes them.</summary>
    private static async Task<string> CellsAsync(Endpoint endpoint, IAuthorizationPolicyProvider policies)
    {
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return string.Join(" | ", Yes, Yes, Yes, Yes);
        }

        AuthorizationPolicy? policy = await AuthorizationPolicy.CombineAsync(
            policies,
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>()).ConfigureAwait(false)
            ?? await policies.GetFallbackPolicyAsync().ConfigureAwait(false);
        if (policy is null)
        {
            return string.Join(" | ", Yes, Yes, Yes, Yes);
        }

        IEnumerable<string> allowed = Roles;
        foreach (RolesAuthorizationRequirement roles in policy.Requirements.OfType<RolesAuthorizationRequirement>())
        {
            allowed = allowed.Intersect(roles.AllowedRoles, StringComparer.Ordinal);
        }

        HashSet<string> allowedRoles = [.. allowed];
        return string.Join(" | ", [No, .. Roles.Select(role => allowedRoles.Contains(role) ? Yes : No)]);
    }

    /// <summary><c>class-sessions/{id:guid}/</c> and <c>/class-sessions/{id}</c> are the same path.</summary>
    private static string NormalisePath(string path)
    {
        string withoutQuery = path.Split('?')[0];
        return "/" + RouteConstraint().Replace(withoutQuery, "{${name}}").Trim('/');
    }

    [GeneratedRegex(@"^`(?<method>GET|POST|PUT|PATCH|DELETE) (?<path>[^`]+)`(?<rest>.*)$", RegexOptions.None, 1000)]
    private static partial Regex EndpointCell();

    [GeneratedRegex(@"\{(?<name>[^}:?=]+)[^}]*\}", RegexOptions.None, 1000)]
    private static partial Regex RouteConstraint();

    private sealed record Row(string Endpoint, string Service, bool Planned, string Cells);
}
