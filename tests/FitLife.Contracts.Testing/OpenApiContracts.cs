using System.Net;
using System.Text.Json;

namespace FitLife.Contracts.Testing;

/// <summary>
/// The provider's OpenAPI contract in <c>contracts/openapi/</c>, for a consumer's tests (#522): every answer the consumer's
/// fake gives must be a route, a method and a status code the contract has, so the fake can't drift from the real service.
/// Read with System.Text.Json: only paths, methods and response codes are needed, and Microsoft.OpenApi isn't a direct
/// package of the repo.
/// </summary>
public static class OpenApiContracts
{
    private const int FirstServerFailure = 500;

    /// <summary>The fake's answers that the provider's contract doesn't have, one line each, without repeats.</summary>
    /// <param name="service">The contract's file name without <c>.json</c>, e.g. <c>membershipservice</c>.</param>
    /// <param name="answers">What the fake answered.</param>
    /// <returns>One line per answer that isn't in the contract; empty when the fake matches it.</returns>
    public static IReadOnlyList<string> Missing(string service, IEnumerable<FakeAnswer> answers)
    {
        return Missing(service, answers, gatewayRoute: string.Empty, notTheProvidersAnswers: []);
    }

    /// <summary>
    /// The fake's answers that the provider's contract doesn't have. A 5xx is never checked: it is a failure the client must
    /// survive, which no contract lists. A code in <paramref name="notTheProvidersAnswers"/> isn't checked either: the test
    /// names it as an answer from something in between, such as the gateway's 404 for a route it doesn't have.
    /// </summary>
    /// <param name="service">The contract's file name without <c>.json</c>, e.g. <c>bookingservice</c>.</param>
    /// <param name="answers">What the fake answered.</param>
    /// <param name="gatewayRoute">The gateway's route in front of the service's own paths, e.g. <c>api/booking</c>; empty without a gateway.</param>
    /// <param name="notTheProvidersAnswers">Codes the fake gives that come from something other than the provider.</param>
    /// <returns>One line per answer that isn't in the contract; empty when the fake matches it.</returns>
    public static IReadOnlyList<string> Missing(
        string service, IEnumerable<FakeAnswer> answers, string gatewayRoute, IReadOnlyCollection<HttpStatusCode> notTheProvidersAnswers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(gatewayRoute);
        ArgumentNullException.ThrowIfNull(notTheProvidersAnswers);
        IReadOnlyList<Operation> operations = Load(service);
        var missing = new List<string>();
        foreach (FakeAnswer answer in answers.Where(answer => IsTheProvidersAnswer(answer, notTheProvidersAnswers)))
        {
            string path = WithoutGatewayRoute(answer.Path, gatewayRoute);
            int status = (int)answer.Status;
            string line = $"{answer.Method} {answer.Path} {status} is not in contracts/openapi/{service}.json";
            if (!operations.Any(operation => operation.Answers(answer.Method, path, status)) && !missing.Contains(line, StringComparer.Ordinal))
            {
                missing.Add(line);
            }
        }

        return missing;
    }

    private static bool IsTheProvidersAnswer(FakeAnswer answer, IReadOnlyCollection<HttpStatusCode> notTheProvidersAnswers)
    {
        return (int)answer.Status < FirstServerFailure && !notTheProvidersAnswers.Contains(answer.Status);
    }

    // /api/booking/class-sessions with the gateway route api/booking is the service's own /class-sessions
    private static string WithoutGatewayRoute(string path, string gatewayRoute)
    {
        string prefix = "/" + gatewayRoute.Trim('/');
        if (prefix.Length > 1 && path.StartsWith(prefix + "/", StringComparison.Ordinal))
        {
            return path[prefix.Length..];
        }

        return path;
    }

    private static List<Operation> Load(string service)
    {
        string file = Path.Combine(AppContext.BaseDirectory, "contracts", "openapi", service + ".json");
        using var contract = JsonDocument.Parse(File.ReadAllText(file));
        var operations = new List<Operation>();
        foreach (JsonProperty path in contract.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty method in path.Value.EnumerateObject().Where(method => method.Value.TryGetProperty("responses", out _)))
            {
                string[] statuses = [.. method.Value.GetProperty("responses").EnumerateObject().Select(response => response.Name)];
                operations.Add(new Operation(method.Name, path.Name.Trim('/').Split('/'), statuses));
            }
        }

        return operations;
    }

    /// <summary>One method on one path of the contract, with the status codes it answers.</summary>
    private sealed record Operation(string Method, string[] Segments, string[] Statuses)
    {
        public bool Answers(HttpMethod method, string path, int status)
        {
            string[] segments = path.Trim('/').Split('/');
            return string.Equals(Method, method.Method, StringComparison.OrdinalIgnoreCase)
                && Statuses.Contains(status.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal)
                && segments.Length == Segments.Length
                && Segments.Zip(segments).All(pair => Matches(pair.First, pair.Second));
        }

        // {accountId} matches any one segment; any other segment matches only itself
        private static bool Matches(string template, string segment)
        {
            if (template.StartsWith('{') && template.EndsWith('}'))
            {
                return segment.Length > 0;
            }

            return string.Equals(template, segment, StringComparison.Ordinal);
        }
    }
}
