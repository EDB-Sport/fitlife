using System.Net;
using System.Text.Json;

namespace FitLife.Configuration;

/// <summary>Reads one KV v2 secret, and waits for Vault while it is starting or not seeded yet.</summary>
internal sealed class VaultClient(VaultSettings settings, HttpMessageHandler handler, TimeProvider time)
{
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    public async Task<IReadOnlyDictionary<string, string?>> ReadSecretAsync(CancellationToken cancellationToken)
    {
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        DateTimeOffset giveUpAt = time.GetUtcNow() + settings.Wait;
        while (true)
        {
            Attempt attempt = await TryReadAsync(invoker, cancellationToken).ConfigureAwait(false);
            if (attempt.Secret is not null)
            {
                return attempt.Secret;
            }

            if (!attempt.WorthWaiting)
            {
                throw new InvalidOperationException($"Found {attempt.Problem} from Vault for the secret {settings.Mount}/{settings.Path}. {Advice}");
            }

            if (time.GetUtcNow() >= giveUpAt)
            {
                throw new InvalidOperationException(
                    $"Found {attempt.Problem} from Vault for the secret {settings.Mount}/{settings.Path} for {settings.Wait.TotalSeconds:0} s "
                    + $"(Vault:WaitSeconds). {Advice}");
            }

            await Task.Delay(Pause, time, cancellationToken).ConfigureAwait(false);
        }
    }

    private const string Advice =
        "Do check Vault:Address, Vault:Token and that the seed job has written the secret (deploy/compose/vault/seed.sh).";

    private async Task<Attempt> TryReadAsync(HttpMessageInvoker invoker, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, settings.SecretUrl);
        request.Headers.Add("X-Vault-Token", settings.Token);
        try
        {
            using HttpResponseMessage response = await invoker.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // 404: not seeded yet. 5xx: starting, sealed or overloaded. Anything else won't change by waiting.
                bool worthWaiting = response.StatusCode == HttpStatusCode.NotFound || (int)response.StatusCode >= 500;
                return new Attempt(null, $"HTTP {(int)response.StatusCode}", worthWaiting);
            }

            Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
                return new Attempt(Values(document), string.Empty, WorthWaiting: false);
            }
        }
        catch (HttpRequestException exception)
        {
            // No answer at all: Vault isn't up yet, or its name doesn't resolve yet
            return new Attempt(null, $"no answer ({exception.Message})", WorthWaiting: true);
        }
    }

    private static Dictionary<string, string?> Values(JsonDocument document)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty value in document.RootElement.GetProperty("data").GetProperty("data").EnumerateObject())
        {
            values[value.Name] = value.Value.ToString();
        }

        return values;
    }

    private sealed record Attempt(IReadOnlyDictionary<string, string?>? Secret, string Problem, bool WorthWaiting);
}
