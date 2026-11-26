using System.Security.Claims;
using System.Text;
using FitLife.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MembershipService.IntegrationTests;

/// <summary>Signs tokens the way Identity does (#169), with a key only the tests use.</summary>
public static class TestTokens
{
    /// <summary>The signing key the test host is started with.</summary>
    public const string SigningKey = "membership-integration-tests-signing-key";

    /// <summary>A key the service doesn't know.</summary>
    public const string OtherKey = "a-key-that-membership-service-never-trusts";

    /// <summary>A token for the account, valid for 30 minutes.</summary>
    public static string For(string accountId, string key = SigningKey)
    {
        return Sign([new Claim(FitLifeJwt.AccountIdClaim, accountId), new Claim(FitLifeJwt.RoleClaim, "Medlem")], key);
    }

    /// <summary>A valid token with a role but no account id.</summary>
    public static string WithoutAccountId()
    {
        return Sign([new Claim(FitLifeJwt.RoleClaim, "Medlem")], SigningKey);
    }

    private static string Sign(Claim[] claims, string key)
    {
        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = FitLifeJwt.Issuer,
            Audience = FitLifeJwt.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.AddMinutes(-1),
            IssuedAt = now.AddMinutes(-1),
            Expires = now.AddMinutes(30),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>A new account id, so no test sees another test's member.</summary>
    public static string NewAccountId()
    {
        return "account-" + Guid.NewGuid().ToString("N");
    }
}
