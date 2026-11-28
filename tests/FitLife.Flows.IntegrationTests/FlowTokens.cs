using System.Security.Claims;
using System.Text;
using FitLife.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FitLife.Flows.IntegrationTests;

/// <summary>Tokens as Identity signs them, with the key <see cref="FlowInfrastructure"/> gives both services.</summary>
internal static class FlowTokens
{
    /// <summary>The key the flow tests sign with.</summary>
    public const string SigningKey = "flow-integration-tests-signing-key";

    /// <summary>A valid token for the account with the role Medlem.</summary>
    /// <param name="accountId">The account.</param>
    /// <returns>The signed token.</returns>
    public static string Medlem(Guid accountId)
    {
        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = FitLifeJwt.Issuer,
            Audience = FitLifeJwt.Audience,
            Subject = new ClaimsIdentity([new Claim(FitLifeJwt.AccountIdClaim, accountId.ToString()), new Claim(FitLifeJwt.RoleClaim, FitLifeRoles.Medlem)]),
            NotBefore = now.AddMinutes(-1),
            IssuedAt = now.AddMinutes(-1),
            Expires = now.AddMinutes(30),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
