using Microsoft.Extensions.Configuration;

namespace FitLife.Configuration;

/// <summary>
/// Loads a service's secret from Vault into <see cref="IConfiguration"/> at startup (course rule #9, #17).
/// Code reads the values like any other setting, for example <c>configuration["MongoDb:Password"]</c>.
/// </summary>
public static class VaultConfigurationExtensions
{
    /// <summary>
    /// Adds the secret at <c>Vault:Mount</c>/<c>Vault:Path</c> (KV v2) as configuration keys. The settings come from
    /// <paramref name="configuration"/> itself (appsettings.json, environment variables such as <c>Vault__Address</c>):
    /// <c>Vault:Address</c>, <c>Vault:Token</c>, <c>Vault:Path</c>, <c>Vault:Mount</c> (default <c>secret</c>) and
    /// <c>Vault:WaitSeconds</c> (default 60). Without <c>Vault:Address</c> nothing is loaded, so the service also runs
    /// without Vault (dotnet run, tests).
    /// </summary>
    /// <remarks>
    /// Compose and Azure start Vault and the service at the same time, and Vault writes the secrets a few seconds
    /// later. So while Vault can't be reached, or answers 404 (not seeded yet) or 5xx, this tries again every second
    /// until <c>Vault:WaitSeconds</c> have passed. Any other answer, such as 403 for a wrong token, fails at once.
    /// </remarks>
    /// <param name="configuration">The service's configuration, as <c>builder.Configuration</c>.</param>
    /// <param name="handler">The HTTP handler that reaches Vault. Leave it out; tests pass a fake one.</param>
    /// <param name="time">The clock for the waits. Leave it out; tests pass a fake one.</param>
    /// <param name="cancellationToken">Stops the waiting.</param>
    /// <returns>The same configuration, with the secret added.</returns>
    /// <exception cref="InvalidOperationException">A setting is missing, or Vault doesn't return the secret in time.</exception>
    public static async Task<IConfigurationManager> AddVaultAsync(
        this IConfigurationManager configuration,
        HttpMessageHandler? handler = null,
        TimeProvider? time = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var settings = VaultSettings.From(configuration);
        if (settings is null)
        {
            return configuration;
        }

        // One read at startup, before dependency injection exists, so there is no IHttpClientFactory to use
        IReadOnlyDictionary<string, string?> secret;
        if (handler is null)
        {
            using var sockets = new SocketsHttpHandler();
            // Stryker disable once Boolean : ConfigureAwait(false) -> true is an equivalent mutant (#270)
            secret = await new VaultClient(settings, sockets, time ?? TimeProvider.System).ReadSecretAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Stryker disable once Boolean : ConfigureAwait(false) -> true is an equivalent mutant (#270)
            secret = await new VaultClient(settings, handler, time ?? TimeProvider.System).ReadSecretAsync(cancellationToken).ConfigureAwait(false);
        }

        configuration.AddInMemoryCollection(secret);
        return configuration;
    }
}
