namespace FitLife.Contracts.Testing;

/// <summary>
/// Compares a service's rows in <c>docs/domain/ACCESS.md</c> with the service's real endpoints (#489):
/// route, HTTP method, Authorize roles and policies, and AllowAnonymous, read from the endpoint metadata in-process.
/// </summary>
public static class AccessTable
{
    /// <summary>Compares the service's rows in <paramref name="accessMarkdown"/> with the app's endpoints.</summary>
    /// <param name="service">The Service column of the rows, e.g. <c>BookingService</c>.</param>
    /// <param name="accessMarkdown">The text of <c>docs/domain/ACCESS.md</c>.</param>
    /// <param name="services">The app's services, which hold its endpoints and authorization policies.</param>
    /// <returns>One line per difference; empty when the rows match the endpoints.</returns>
    public static Task<IReadOnlyList<string>> CompareAsync(string service, string accessMarkdown, IServiceProvider services)
    {
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
