namespace BookingService.Jobs;

/// <summary>Registers <see cref="WaitlistClosingJob"/> where the configuration asks for it (#314).</summary>
public static class WaitlistClosingServiceCollectionExtensions
{
    /// <summary>Adds the job when <c>WaitlistClosing:Enabled</c> is true (compose sets it); otherwise adds nothing.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The service's configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddWaitlistClosing(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("WaitlistClosing:Enabled"))
        {
            services.AddHostedService<WaitlistClosingJob>();
        }

        return services;
    }
}
