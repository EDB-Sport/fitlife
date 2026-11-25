using System.Security.Claims;
using System.Text;
using FitLife.Authentication;
using IdentityService.Domain;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Application;

/// <summary>
/// Writes the token for an account with the <see cref="FitLifeJwt"/> values, so every service checks it the same way
/// (CONTEXT-MAP section 4): the account's id in <c>sub</c>, one <c>role</c> claim per role, HMAC-SHA256.
/// </summary>
public sealed class TokenWriter
{
    /// <summary>How long a token is accepted.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    private readonly IConfiguration _configuration;
    private readonly TimeProvider _time;

    /// <summary>Creates the writer.</summary>
    /// <param name="configuration">Holds the signing key at <see cref="FitLifeJwt.SigningKeyConfigurationKey"/>, read when a token is written.</param>
    /// <param name="time">The clock the token's times come from.</param>
    public TokenWriter(IConfiguration configuration, TimeProvider time)
    {
        _configuration = configuration;
        _time = time;
    }

    /// <summary>Writes a token for <paramref name="account"/>, valid from now for <see cref="Lifetime"/>.</summary>
    /// <param name="account">The account that logged in.</param>
    /// <returns>The token and when it expires.</returns>
    /// <exception cref="InvalidOperationException">The signing key is missing or shorter than <see cref="FitLifeJwt.MinimumSigningKeyBytes"/> bytes.</exception>
    public LogInResult Write(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var key = new SymmetricSecurityKey(SigningKey());
        DateTimeOffset now = _time.GetUtcNow();
        DateTimeOffset expiresAt = now.Add(Lifetime);

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(FitLifeJwt.AccountIdClaim, account.Id.ToString()));
        identity.AddClaims(account.Roles.Select(role => new Claim(FitLifeJwt.RoleClaim, role.Name)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = FitLifeJwt.Issuer,
            Audience = FitLifeJwt.Audience,
            Subject = identity,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        return new LogInResult(new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }

    private byte[] SigningKey()
    {
        string? key = _configuration[FitLifeJwt.SigningKeyConfigurationKey];
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                $"Found no {FitLifeJwt.SigningKeyConfigurationKey} in configuration. Set it through Vault (JWT_SIGNING_KEY, deploy/compose/README.md) or, in tests, in the test configuration.");
        }

        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        if (keyBytes.Length < FitLifeJwt.MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Found a {FitLifeJwt.SigningKeyConfigurationKey} of {keyBytes.Length} bytes. Use at least {FitLifeJwt.MinimumSigningKeyBytes} bytes for HMAC-SHA256.");
        }

        return keyBytes;
    }
}
