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

    /// <summary>The membership of the account in the cookie's token, with <c>GET api/membership/members/me</c>.</summary>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The membership, or <see langword="null"/> on a 404: the account has not joined.</returns>
    /// <exception cref="MembershipUnavailableException">Membership answered another error, a body that isn't a membership, or nothing.</exception>
    Task<MyMembershipResponse?> GetMyMembershipAsync(CancellationToken cancellationToken);

    /// <summary>The account in the cookie's token pays the missing fee with <c>POST api/membership/members/me/payment</c>, without a body.</summary>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The membership after a 200; on a 409 its <c>code</c>; on a 404 or a 409 without a code, neither.</returns>
    /// <exception cref="MembershipUnavailableException">Membership answered another error, a body that isn't a membership, or nothing.</exception>
    Task<PaymentAnswer> PayAsync(CancellationToken cancellationToken);
}
