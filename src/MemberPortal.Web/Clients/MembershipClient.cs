using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MemberPortal.Web.Clients;

/// <summary>
/// Membership's <c>POST members</c>, reached through the gateway's route <c>api/membership</c>, over a typed client.
/// A 201 is read for the membership's status, a 409 is already a member; every other failure becomes a <see cref="MembershipUnavailableException"/>.
/// </summary>
/// <param name="http">A client with the gateway's base address and the <see cref="BearerTokenHandler"/>, from <c>IHttpClientFactory</c>.</param>
public sealed class MembershipClient(HttpClient http) : IMembershipClient
{
    private const string MembersPath = "api/membership/members";

    private const string MyMembershipPath = "api/membership/members/me";

    private const string PaymentPath = "api/membership/members/me/payment";

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
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // Membership's 409 carries no code (contracts/openapi/membershipservice.json): it means already a member
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

    /// <inheritdoc />
    public async Task<MyMembershipResponse?> GetMyMembershipAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await http.GetAsync(new Uri(MyMembershipPath, UriKind.Relative), cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // The account has not joined (contracts/openapi/membershipservice.json)
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MyMembershipResponse>(cancellationToken).ConfigureAwait(false)
                ?? throw new MembershipUnavailableException("Membership answered no membership.");
        }
        catch (JsonException exception)
        {
            throw new MembershipUnavailableException("Membership answered a body that isn't a membership.", exception);
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

    /// <inheritdoc />
    public async Task<PaymentAnswer> PayAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await http.PostAsync(new Uri(PaymentPath, UriKind.Relative), content: null, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return new PaymentAnswer(Membership: null, await RejectionCodeAsync(response, cancellationToken).ConfigureAwait(false));
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // The account has not joined: nothing the member can pay
                return new PaymentAnswer(Membership: null, RejectionCode: null);
            }

            response.EnsureSuccessStatusCode();
            MyMembershipResponse membership = await response.Content.ReadFromJsonAsync<MyMembershipResponse>(cancellationToken).ConfigureAwait(false)
                ?? throw new MembershipUnavailableException("Membership answered no membership.");
            return new PaymentAnswer(membership, RejectionCode: null);
        }
        catch (JsonException exception)
        {
            throw new MembershipUnavailableException("Membership answered a body that isn't a membership.", exception);
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

    // The problem details' "code" extension (Membership's MemberEndpoints), or null when the body has none
    private static async Task<string?> RejectionCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken).ConfigureAwait(false);
            if (problem is not null && problem.Extensions.TryGetValue("code", out object? code) && code is JsonElement { ValueKind: JsonValueKind.String } text)
            {
                return text.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
