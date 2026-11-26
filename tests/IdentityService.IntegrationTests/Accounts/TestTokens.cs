using System.Security.Claims;
using System.Text;
using FitLife.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.IntegrationTests.Accounts;

/// <summary>Signs tokens the way Identity does (#235), with the key <see cref="IdentityApiFactory"/> starts the app with, so an endpoint test needs no log-in first.</summary>
internal static class TestTokens
{
    /// <summary>A token for a new account with the one role, valid for 30 minutes.</summary>
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
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(IdentityApiFactory.SigningKey)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
