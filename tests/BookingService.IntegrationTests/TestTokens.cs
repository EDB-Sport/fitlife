using System.Security.Claims;
using System.Text;
using FitLife.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BookingService.IntegrationTests;

/// <summary>Tokens signed with the test key, as IdentityService would issue them (#260, as tests/FitLife.Authentication.Tests).</summary>
internal static class TestTokens
{
    /// <summary>The key every BookingService in these tests trusts (<see cref="JwtSigningKeySetUp"/>).</summary>
    public const string SigningKey = "booking-integration-tests-signing-key";

    /// <summary>A token for the account with the given role.</summary>
    /// <param name="accountId">The account id, the <c>sub</c> claim.</param>
    /// <param name="role">The role claim.</param>
    /// <returns>The signed token.</returns>
    public static string For(Guid accountId, string role = FitLifeRoles.Medlem)
    {
        return For(accountId.ToString(), role);
    }

    /// <summary>A token whose <c>sub</c> is any text, e.g. one that is not an account id.</summary>
    /// <param name="accountId">The <c>sub</c> claim.</param>
    /// <param name="role">The role claim.</param>
    /// <returns>The signed token.</returns>
    public static string For(string accountId, string role = FitLifeRoles.Medlem)
    {
        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = FitLifeJwt.Issuer,
            Audience = FitLifeJwt.Audience,
            Subject = new ClaimsIdentity([new Claim(FitLifeJwt.AccountIdClaim, accountId), new Claim(FitLifeJwt.RoleClaim, role)]),
            NotBefore = now.AddMinutes(-1),
            IssuedAt = now.AddMinutes(-1),
            Expires = now.AddMinutes(30),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
