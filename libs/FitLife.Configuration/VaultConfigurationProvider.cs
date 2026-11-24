using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace FitLife.Configuration;

/// <summary>Reads the secret once, when the configuration is built. Its keys are configuration keys (deploy/compose/vault/seed.sh).</summary>
internal sealed class VaultConfigurationProvider(VaultSettings settings, HttpMessageHandler? handler) : ConfigurationProvider
{
    public override void Load()
    {
        // One request at startup, before dependency injection exists, so there is no IHttpClientFactory to use
        if (handler is not null)
        {
            Data = Read(handler);
            return;
        }

        using var sockets = new SocketsHttpHandler();
        Data = Read(sockets);
    }

    private Dictionary<string, string?> Read(HttpMessageHandler vault)
    {
        using var invoker = new HttpMessageInvoker(vault, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, settings.SecretUrl);
        request.Headers.Add("X-Vault-Token", settings.Token);

        using HttpResponseMessage response = invoker.Send(request, CancellationToken.None);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Found HTTP {(int)response.StatusCode} from Vault for the secret {settings.Mount}/{settings.Path}. "
                + "Do check Vault:Address, Vault:Token and that the seed job has written the secret (deploy/compose/vault/seed.sh).");
        }

        using Stream body = response.Content.ReadAsStream();
        using var document = JsonDocument.Parse(body);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty value in document.RootElement.GetProperty("data").GetProperty("data").EnumerateObject())
        {
            values[value.Name] = value.Value.ToString();
        }

        return values;
    }
}
