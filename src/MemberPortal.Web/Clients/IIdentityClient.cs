namespace MemberPortal.Web.Clients;

/// <summary>Identity, reached through the gateway.</summary>
public interface IIdentityClient
{
    /// <summary>Logs in with <c>POST api/identity/tokens</c>.</summary>
    /// <param name="request">The e-mail address and the password.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The token, or <see langword="null"/> when the e-mail address or the password is wrong (401).</returns>
    /// <exception cref="IdentityUnavailableException">Identity answered another error, an answer that can't be read, or nothing.</exception>
    Task<TokenResponse?> LogInAsync(LogInRequest request, CancellationToken cancellationToken);
}
