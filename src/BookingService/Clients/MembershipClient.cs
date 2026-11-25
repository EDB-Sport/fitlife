using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookingService.Application;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace BookingService.Clients;

/// <summary>
/// Membership's lookup by account id (#170's <c>GET members/by-account/{accountId}</c>), over a typed client with the standard
/// resilience handler (#256). Every failure becomes <see cref="MemberLookup.Unavailable"/>, never an exception.
/// </summary>
/// <param name="http">A client with Membership's base address, from <c>IHttpClientFactory</c>.</param>
public sealed class MembershipClient(HttpClient http) : IMembershipClient
{
    /// <summary>The configuration key of Membership's address.</summary>
    public const string BaseUrlConfigurationKey = "Membership:BaseUrl";

    /// <inheritdoc />
    public async Task<MemberLookup> FindMemberAsync(Guid accountId, string bearerToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"members/by-account/{accountId}", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return MemberLookup.NotAMember;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return MemberLookup.Unavailable;
            }

            MemberResponse? member = await response.Content.ReadFromJsonAsync<MemberResponse>(cancellationToken).ConfigureAwait(false);
            if (member is null)
            {
                return MemberLookup.Unavailable;
            }

            return MemberLookup.Found(member.MemberId, member.IsActive);
        }
        catch (HttpRequestException)
        {
            // No connection to Membership
            return MemberLookup.Unavailable;
        }
        catch (BrokenCircuitException)
        {
            // The circuit is open after too many failures, so Membership isn't asked at all
            return MemberLookup.Unavailable;
        }
        catch (TimeoutRejectedException)
        {
            // The resilience handler's time-out: 30 s in all by default, well within HttpClient's own 100 s. The caller's own
            // cancellation is passed on
            return MemberLookup.Unavailable;
        }
        catch (JsonException)
        {
            // An answer we can't read
            return MemberLookup.Unavailable;
        }
    }

    /// <summary>The two fields of Membership's answer that Booking reads.</summary>
    /// <param name="MemberId">The member id.</param>
    /// <param name="IsActive">Whether the membership is active.</param>
    private sealed record MemberResponse(Guid MemberId, bool IsActive);
}
