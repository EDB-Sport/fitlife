namespace IdentityService.Application;

/// <summary>
/// Runs <see cref="DemoAccountSeeder"/> after start-up when <see cref="DemoAccountSeeder.EnabledConfigurationKey"/> is true,
/// so start-up isn't blocked. A failed seed is logged as an error, and the service keeps running.
/// </summary>
public sealed class DemoAccountSeedService : BackgroundService
{
    private readonly Lazy<DemoAccountSeeder> _seeder;
    private readonly DemoAccountSeedStatus _status;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DemoAccountSeedService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="seeder">Creates the demo accounts. Lazy, so the repository (and its MongoDB settings) is resolved only when the seed runs.</param>
    /// <param name="status">Records when the seed has finished.</param>
    /// <param name="configuration">Holds <see cref="DemoAccountSeeder.EnabledConfigurationKey"/>.</param>
    /// <param name="logger">Logs a failed seed.</param>
    public DemoAccountSeedService(Lazy<DemoAccountSeeder> seeder, DemoAccountSeedStatus status, IConfiguration configuration, ILogger<DemoAccountSeedService> logger)
    {
        _seeder = seeder;
        _status = status;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>(DemoAccountSeeder.EnabledConfigurationKey))
        {
            return;
        }

        try
        {
            await _seeder.Value.SeedAsync(stoppingToken).ConfigureAwait(false);
            _status.MarkFinished();
        }
        catch (Exception exception) when (exception is not OperationCanceledException) // a failed seed must not stop the service
        {
            _logger.LogError(exception, "The demo accounts weren't seeded");
        }
    }
}
