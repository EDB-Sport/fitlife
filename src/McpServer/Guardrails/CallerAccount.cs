using FitLife.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace McpServer.Guardrails;

/// <summary>
/// The account a request to <c>/mcp</c> comes from: the <see cref="FitLifeJwt.AccountIdClaim"/> claim of its bearer token (#216).
/// The signature isn't checked: the server holds no signing key, and the account id only picks the rate limit and goes in the call log.
/// Booking checks every forwarded token, so a forged one gets nothing there.
/// </summary>
public static class CallerAccount
{
    private const string BearerPrefix = "Bearer ";

    /// <summary>Reads the account id from an <c>Authorization</c> header.</summary>
    /// <param name="authorizationHeader">The header's value, or <see langword="null"/> when the request has none.</param>
    /// <returns>The account id, or <see langword="null"/> when the header holds no bearer token with a <c>sub</c>.</returns>
    public static string? AccountIdFrom(string? authorizationHeader)
    {
        if (authorizationHeader is null || !authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        JsonWebToken token;
        try
        {
            token = new JsonWebToken(authorizationHeader[BearerPrefix.Length..]);
        }
        catch (ArgumentException)
        {
            // Not a JWT: no three parts, or a part that isn't base64url JSON (SecurityTokenMalformedException is an ArgumentException)
            return null;
        }

        if (!token.TryGetPayloadValue(FitLifeJwt.AccountIdClaim, out string accountId) || string.IsNullOrEmpty(accountId))
        {
            return null;
        }

        return accountId;
    }
}
