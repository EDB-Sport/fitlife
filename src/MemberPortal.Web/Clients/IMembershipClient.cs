namespace MemberPortal.Web.Clients;

/// <summary>Membership, reached through the gateway.</summary>
public interface IMembershipClient
{
    /// <summary>The account in the token joins with <c>POST api/membership/members</c>.</summary>
    /// <param name="accessToken">
    /// The token to send. <see langword="null"/> sends the cookie's token through the <see cref="BearerTokenHandler"/>;
    /// right after logging in the request has no cookie yet, so the fresh token is passed in.
    /// </param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>
    /// <see cref="JoinMembershipAnswer.Joined"/>, <see cref="JoinMembershipAnswer.PaymentMissing"/> when the first fee isn't paid,
    /// or <see cref="JoinMembershipAnswer.AlreadyAMember"/> on a 409.
    /// </returns>
    /// <exception cref="MembershipUnavailableException">Membership answered another error, a body that isn't a member, or nothing.</exception>
    Task<JoinMembershipAnswer> JoinAsync(string? accessToken, CancellationToken cancellationToken);
}
