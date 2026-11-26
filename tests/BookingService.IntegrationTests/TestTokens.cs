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

    /// <summary>A key the service doesn't know.</summary>
    public const string OtherKey = "a-key-that-booking-service-never-trusts";

    /// <summary>A token for the account with the given role.</summary>
    /// <param name="accountId">The account id, the <c>sub</c> claim.</param>
    /// <param name="role">The role claim.</param>
    /// <param name="key">The key the token is signed with.</param>
    /// <returns>The signed token.</returns>
    public static string For(Guid accountId, string role = FitLifeRoles.Medlem, string key = SigningKey)
    {
        return For(accountId.ToString(), role, key);
    }

    /// <summary>A token whose <c>sub</c> is any text, e.g. one that is not an account id.</summary>
    /// <param name="accountId">The <c>sub</c> claim.</param>
    /// <param name="role">The role claim.</param>
    /// <param name="key">The key the token is signed with.</param>
    /// <returns>The signed token.</returns>
    public static string For(string accountId, string role = FitLifeRoles.Medlem, string key = SigningKey)
    {
        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        return Sign(accountId, role, key, notBefore: now.AddMinutes(-1), expires: now.AddMinutes(30));
    }

    /// <summary>A token that was valid for 30 minutes and ran out 1 minute ago.</summary>
    /// <param name="accountId">The account id, the <c>sub</c> claim.</param>
    /// <param name="role">The role claim.</param>
    /// <returns>The signed token.</returns>
    public static string Expired(Guid accountId, string role = FitLifeRoles.Medlem)
    {
        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        return Sign(accountId.ToString(), role, SigningKey, notBefore: now.AddMinutes(-31), expires: now.AddMinutes(-1));
    }

    private static string Sign(string accountId, string role, string key, DateTime notBefore, DateTime expires)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = FitLifeJwt.Issuer,
            Audience = FitLifeJwt.Audience,
            Subject = new ClaimsIdentity([new Claim(FitLifeJwt.AccountIdClaim, accountId), new Claim(FitLifeJwt.RoleClaim, role)]),
            NotBefore = notBefore,
            IssuedAt = notBefore,
            Expires = expires,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
