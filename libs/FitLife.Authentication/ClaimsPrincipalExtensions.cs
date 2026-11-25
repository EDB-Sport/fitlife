using System.Security.Claims;

namespace FitLife.Authentication;

/// <summary>Reads the token's claims, the same way in every service (#362).</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The id of the signed-in <c>Account</c>: the <see cref="FitLifeJwt.AccountIdClaim"/> claim, as the string Membership stores.
    /// Call it only on an endpoint that requires a token (the fallback policy of
    /// <see cref="AuthorizationExtensions.AddFitLifeAuthorization"/>); an endpoint marked <c>AllowAnonymous()</c>, such as
    /// <c>/health</c> or the OpenAPI document and Scalar in Development, has no account.
    /// </summary>
    /// <param name="user">The signed-in user.</param>
    /// <returns>The account id.</returns>
    /// <exception cref="InvalidOperationException">The user has no account id claim, so no token was checked.</exception>
    public static string GetAccountId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        string? accountId = user.FindFirstValue(FitLifeJwt.AccountIdClaim);
        if (string.IsNullOrEmpty(accountId))
        {
            throw new InvalidOperationException(
                $"Found no '{FitLifeJwt.AccountIdClaim}' claim. Read the account id only on an endpoint that requires a token.");
        }

        return accountId;
    }
}
