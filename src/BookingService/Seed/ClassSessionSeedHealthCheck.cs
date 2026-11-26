using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BookingService.Seed;

/// <summary>
/// Unhealthy until the demo data is loaded (#255), so <c>docker compose up --wait</c> and api-tests wait for it. Registered only
/// with <c>Seed:Enabled</c>, as the seeder is. A failed seed leaves it unhealthy, as Identity's demo accounts do (#237).
/// </summary>
public sealed class ClassSessionSeedHealthCheck : IHealthCheck
{
    private readonly ClassSessionSeedStatus _status;

    /// <summary>Creates the check.</summary>
    /// <param name="status">Whether the seed has finished.</param>
    public ClassSessionSeedHealthCheck(ClassSessionSeedStatus status)
    {
        _status = status;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_status.HasFinished)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("The demo data isn't loaded yet."));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
