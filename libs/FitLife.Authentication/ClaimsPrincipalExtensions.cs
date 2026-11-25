using System.Security.Claims;

namespace FitLife.Authentication;

/// <summary>Reads the token's claims, the same way in every service (#362).</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Not built yet (#362).</summary>
    /// <param name="user">The signed-in user.</param>
    /// <returns>The account id.</returns>
    public static string GetAccountId(this ClaimsPrincipal user)
    {
        return string.Empty;
    }
}
