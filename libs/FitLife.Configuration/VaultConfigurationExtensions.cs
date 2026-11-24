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
    /// <c>Vault:Address</c>, <c>Vault:Token</c>, <c>Vault:Path</c>, and <c>Vault:Mount</c> (default <c>secret</c>).
    /// Without <c>Vault:Address</c> nothing is loaded, so the service also runs without Vault (dotnet run, tests).
    /// </summary>
    /// <param name="configuration">The service's configuration, as <c>builder.Configuration</c>.</param>
    /// <param name="handler">The HTTP handler that reaches Vault. Leave it out; tests pass a fake one.</param>
    /// <returns>The same configuration, with the secret added.</returns>
    /// <exception cref="InvalidOperationException">A setting is missing, or Vault doesn't return the secret.</exception>
    public static IConfigurationManager AddVault(this IConfigurationManager configuration, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var settings = VaultSettings.From(configuration);
        if (settings is null)
        {
            return configuration;
        }

        configuration.Add(new VaultConfigurationSource(settings, handler));
        return configuration;
    }
}
