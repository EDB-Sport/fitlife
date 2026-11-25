using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityService.Application;

/// <summary>Unhealthy while <see cref="DemoAccountSeeder.EnabledConfigurationKey"/> is true and the demo accounts aren't seeded, so docker compose --wait and api-tests wait for them.</summary>
public sealed class DemoAccountSeedHealthCheck : IHealthCheck
{
    private readonly DemoAccountSeedStatus _status;
    private readonly IConfiguration _configuration;

    /// <summary>Creates the check.</summary>
    /// <param name="status">Whether the seed has finished.</param>
    /// <param name="configuration">Holds <see cref="DemoAccountSeeder.EnabledConfigurationKey"/>.</param>
    public DemoAccountSeedHealthCheck(DemoAccountSeedStatus status, IConfiguration configuration)
    {
        _status = status;
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_configuration.GetValue<bool>(DemoAccountSeeder.EnabledConfigurationKey) && !_status.HasFinished)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("The demo accounts aren't seeded yet."));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
