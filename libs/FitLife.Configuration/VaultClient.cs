using System.Net;
using System.Text.Json;

namespace FitLife.Configuration;

/// <summary>Reads one KV v2 secret, and waits for Vault while it is starting or not seeded yet.</summary>
internal sealed class VaultClient(VaultSettings settings, HttpMessageHandler handler, TimeProvider time)
{
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    // One attempt that gets no answer in this time is cancelled, so Vault:WaitSeconds stays an upper bound
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(5);

    public async Task<IReadOnlyDictionary<string, string?>> ReadSecretAsync(CancellationToken cancellationToken)
    {
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        DateTimeOffset giveUpAt = time.GetUtcNow() + settings.Wait;
        while (true)
        {
            Attempt attempt = await TryReadWithTimeoutAsync(invoker, giveUpAt - time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
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
                throw GaveUp(attempt);
            }

            await Task.Delay(Pause, time, cancellationToken).ConfigureAwait(false);
            if (time.GetUtcNow() >= giveUpAt)
            {
                // The pause used up the time: another attempt would run past Vault:WaitSeconds
                throw GaveUp(attempt);
            }
        }
    }

    private const string Advice =
        "Do check Vault:Address, Vault:Token and that the seed job has written the secret (deploy/compose/vault/seed.sh).";

    private InvalidOperationException GaveUp(Attempt attempt)
    {
        return new InvalidOperationException(
            $"Found {attempt.Problem} from Vault for the secret {settings.Mount}/{settings.Path} for {settings.Wait.TotalSeconds:0} s "
            + $"(Vault:WaitSeconds). {Advice}");
    }

    private async Task<Attempt> TryReadWithTimeoutAsync(HttpMessageInvoker invoker, TimeSpan remaining, CancellationToken cancellationToken)
    {
        TimeSpan timeout = AttemptTimeout;
        if (remaining > TimeSpan.Zero && remaining < AttemptTimeout)
        {
            timeout = remaining;
        }

        using var attemptTimeout = new CancellationTokenSource(timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, attemptTimeout.Token);
        return await TryReadAsync(invoker, linked.Token, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Attempt> TryReadAsync(
        HttpMessageInvoker invoker, CancellationToken attemptToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, settings.SecretUrl);
        request.Headers.Add("X-Vault-Token", settings.Token);
        try
        {
            using HttpResponseMessage response = await invoker.SendAsync(request, attemptToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // 404: not seeded yet. 5xx: starting, sealed or overloaded. Anything else won't change by waiting.
                bool worthWaiting = response.StatusCode == HttpStatusCode.NotFound || (int)response.StatusCode >= 500;
                return new Attempt(null, $"HTTP {(int)response.StatusCode}", worthWaiting);
            }

            Stream body = await response.Content.ReadAsStreamAsync(attemptToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: attemptToken).ConfigureAwait(false);
                return new Attempt(Values(document), string.Empty, WorthWaiting: false);
            }
        }
        catch (HttpRequestException exception)
        {
            // No answer at all: Vault isn't up yet, or its name doesn't resolve yet
            return new Attempt(null, $"no answer ({exception.Message})", WorthWaiting: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Only the attempt's own time-out fired: Vault accepted the connection and did not reply
            return new Attempt(null, "no answer (time-out)", WorthWaiting: true);
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
