using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MembershipService.Domain;

namespace MembershipService.PaymentProvider;

/// <summary>
/// The anticorruption layer to the external payment provider (#174, CONTEXT-MAP section 4). The only class that knows the
/// provider's charges API and its field names. In the PoC the provider is a Postman Mock Server; its address is
/// <c>PaymentProvider:BaseUrl</c> in configuration (TEAM010).
/// </summary>
/// <param name="http">A client with the provider's base address, from <c>IHttpClientFactory</c>.</param>
public sealed class PaymentProviderClient(HttpClient http) : IPaymentProvider
{
    /// <summary>The configuration key of the provider's address.</summary>
    public const string BaseUrlConfigurationKey = "PaymentProvider:BaseUrl";

    private static readonly Uri Charges = new("/v1/charges", UriKind.Relative);

    /// <inheritdoc />
    public async Task<bool> ChargeMembershipFeeAsync(Guid memberId, decimal amountDkk, CancellationToken cancellationToken)
    {
        var charge = new ChargeRequest(
            memberId.ToString(),
            decimal.ToInt64(decimal.Round(amountDkk * 100m, MidpointRounding.AwayFromZero)),
            "DKK",
            "FitLife membership fee");
        try
        {
            using HttpResponseMessage response = await http.PostAsJsonAsync(Charges, charge, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Declined (402) or a provider error (5xx): either way the fee is not paid
                return false;
            }

            using JsonDocument result = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return result.RootElement.TryGetProperty("outcome", out JsonElement outcome)
                && outcome.ValueKind == JsonValueKind.String
                && string.Equals(outcome.GetString(), "approved", StringComparison.Ordinal);
        }
        catch (HttpRequestException)
        {
            // The provider can't be reached, so nothing was charged
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's own timeout: no answer, so the fee is not paid. The caller's cancellation is passed on
            return false;
        }
        catch (JsonException)
        {
            // An answer we can't read is not an approved charge
            return false;
        }
    }

    private sealed record ChargeRequest(
        [property: JsonPropertyName("customer_reference")] string CustomerReference,
        [property: JsonPropertyName("amount_minor")] long AmountMinor,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("description")] string Description);
}
