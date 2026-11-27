namespace MemberPortal.Web.Clients;

/// <summary>Identity, reached through the gateway.</summary>
public interface IIdentityClient
{
    /// <summary>Creates an account with <c>POST api/identity/accounts</c>.</summary>
    /// <param name="request">The e-mail address and the password.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>Whether the account is made, the e-mail address is taken (409), or Identity refuses the e-mail address or the password (400).</returns>
    /// <exception cref="IdentityUnavailableException">Identity answered another error, or nothing.</exception>
    Task<CreateAccountAnswer> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken);

    /// <summary>Logs in with <c>POST api/identity/tokens</c>.</summary>
    /// <param name="request">The e-mail address and the password.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The token, or <see langword="null"/> when the e-mail address or the password is wrong (401).</returns>
    /// <exception cref="IdentityUnavailableException">Identity answered another error, an answer that can't be read, or nothing.</exception>
    Task<TokenResponse?> LogInAsync(LogInRequest request, CancellationToken cancellationToken);
}
