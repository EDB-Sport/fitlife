using System.Net;
using System.Net.Http.Headers;

namespace MemberPortal.Web.Clients;

/// <summary>
/// Membership's <c>POST members</c>, reached through the gateway's route <c>api/membership</c>, over a typed client.
/// A 409 is already a member; every other failure becomes a <see cref="MembershipUnavailableException"/>.
/// </summary>
/// <param name="http">A client with the gateway's base address and the <see cref="BearerTokenHandler"/>, from <c>IHttpClientFactory</c>.</param>
public sealed class MembershipClient(HttpClient http) : IMembershipClient
{
    private const string MembersPath = "api/membership/members";

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
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // Membership's 409 carries no code (contracts/openapi/membershipservice.json): it means already a member
                return JoinMembershipAnswer.AlreadyAMember;
            }

            response.EnsureSuccessStatusCode();
            return JoinMembershipAnswer.Joined;
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
