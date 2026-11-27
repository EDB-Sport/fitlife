using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MemberPortal.Web.Clients;

/// <summary>
/// Membership's <c>POST members</c>, reached through the gateway's route <c>api/membership</c>, over a typed client.
/// A 201 is read for the membership's status, a 409 with <c>code</c> <c>AccountIsAlreadyAMember</c> is already a member (#542); every other failure becomes a <see cref="MembershipUnavailableException"/>.
/// </summary>
/// <param name="http">A client with the gateway's base address and the <see cref="BearerTokenHandler"/>, from <c>IHttpClientFactory</c>.</param>
public sealed class MembershipClient(HttpClient http) : IMembershipClient
{
    private const string MembersPath = "api/membership/members";

    private const string AccountIsAlreadyAMemberCode = "AccountIsAlreadyAMember";
    // MembershipStatus.PaymentMissing in contracts/openapi/membershipservice.json
    private const string PaymentMissing = "PaymentMissing";

    /// <inheritdoc />
    public async Task<JoinMembershipAnswer> JoinAsync(string? accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(MembersPath, UriKind.Relative));
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict
                && await ProblemCode.HasCode(response, AccountIsAlreadyAMemberCode, cancellationToken).ConfigureAwait(false))
            {
                return JoinMembershipAnswer.AlreadyAMember;
            }

            response.EnsureSuccessStatusCode();
            MemberResponse? member = await response.Content.ReadFromJsonAsync<MemberResponse>(cancellationToken).ConfigureAwait(false);
            if (member is null)
            {
                throw new MembershipUnavailableException("Membership answered no member.");
            }

            if (string.Equals(member.MembershipStatus, PaymentMissing, StringComparison.Ordinal))
            {
                return JoinMembershipAnswer.PaymentMissing;
            }

            return JoinMembershipAnswer.Joined;
        }
        catch (JsonException exception)
        {
            throw new MembershipUnavailableException("Membership answered a body that isn't a member.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new MembershipUnavailableException("Membership can't be reached, or answered an error.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's timeout. The caller's own cancellation passes on untouched
            throw new MembershipUnavailableException("Membership didn't answer in time.", exception);
        }
    }
}
