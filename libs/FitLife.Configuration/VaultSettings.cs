using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace FitLife.Configuration;

/// <summary>Where the service's secret is in Vault, read from the <c>Vault</c> configuration section.</summary>
internal sealed record VaultSettings(Uri Address, string Token, string Mount, string Path, TimeSpan Wait)
{
    private const string Section = "Vault";
    private const int DefaultWaitSeconds = 60;

    /// <summary>The URL of the secret in the KV v2 engine: <c>{Address}/v1/{Mount}/data/{Path}</c>.</summary>
    public Uri SecretUrl => new(Address, $"v1/{Mount}/data/{Path}");

    /// <summary>The settings, or null when <c>Vault:Address</c> is not set.</summary>
    public static VaultSettings? From(IConfiguration configuration)
    {
        IConfigurationSection vault = configuration.GetSection(Section);
        string? address = vault["Address"];
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        string mount = vault["Mount"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(mount))
        {
            mount = "secret";
        }

        // A trailing slash, so a path in the address (a proxy, for example) is kept
        string root = address;
        if (!root.EndsWith('/'))
        {
            root += "/";
        }

        int waitSeconds = DefaultWaitSeconds;
        if (int.TryParse(vault["WaitSeconds"], NumberStyles.None, CultureInfo.InvariantCulture, out int configured))
        {
            waitSeconds = configured;
        }

        return new VaultSettings(
            new Uri(root, UriKind.Absolute), Required(vault, "Token"), mount, Required(vault, "Path"), TimeSpan.FromSeconds(waitSeconds));
    }

    private static string Required(IConfigurationSection vault, string key)
    {
        string? value = vault[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Found Vault:Address but no {Section}:{key}. Do set {Section}:{key} (environment variable {Section}__{key}) as well.");
        }

        return value;
    }
}
