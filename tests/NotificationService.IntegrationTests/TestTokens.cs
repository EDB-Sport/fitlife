using System.Security.Claims;
using System.Text;
using FitLife.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace NotificationService.IntegrationTests;

/// <summary>Tokens signed with the test key, as IdentityService would issue them (#221, as in BookingService.IntegrationTests).</summary>
internal static class TestTokens
{
    /// <summary>The key every NotificationService in these tests trusts (<see cref="JwtSigningKeySetUp"/>).</summary>
    public const string SigningKey = "notification-integration-tests-signing-key";

    /// <summary>A token for a new account with the given role.</summary>
    /// <param name="role">The role claim.</param>
    /// <returns>The signed token.</returns>
    public static string For(string role)
    {
        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = FitLifeJwt.Issuer,
            Audience = FitLifeJwt.Audience,
            Subject = new ClaimsIdentity([new Claim(FitLifeJwt.AccountIdClaim, Guid.NewGuid().ToString()), new Claim(FitLifeJwt.RoleClaim, role)]),
            NotBefore = now.AddMinutes(-1),
            IssuedAt = now.AddMinutes(-1),
            Expires = now.AddMinutes(30),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
