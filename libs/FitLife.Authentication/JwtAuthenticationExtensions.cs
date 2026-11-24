using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FitLife.Authentication;

/// <summary>Sets up the check of the token from Identity, the same way in every service.</summary>
public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Adds JWT bearer authentication as the default scheme: HMAC-SHA256 with the key from
    /// <see cref="FitLifeJwt.SigningKeyConfigurationKey"/>, issuer <see cref="FitLifeJwt.Issuer"/>,
    /// audience <see cref="FitLifeJwt.Audience"/>, and the lifetime checked. The claims keep their names from the token.
    /// A missing or too short key stops the service when it starts (on the first request during build-time OpenAPI generation).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddFitLifeJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        OptionsBuilder<JwtBearerOptions> options = services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration>(Configure);

        // The build writes contracts/openapi/<service>.json by starting the app in GetDocument.Insider, without Vault.
        // There the key is checked on the first request instead, which never comes.
        if (!IsBuildTimeDocumentGeneration())
        {
            options.ValidateOnStart();
        }

        return services;
    }

    // The check Microsoft documents for build-time OpenAPI generation (aspnetcore openapi docs, "build-time document generation")
    private static bool IsBuildTimeDocumentGeneration()
    {
        return string.Equals(Assembly.GetEntryAssembly()?.GetName().Name, "GetDocument.Insider", StringComparison.Ordinal);
    }

    private static void Configure(JwtBearerOptions options, IConfiguration configuration)
    {
        string? key = configuration[FitLifeJwt.SigningKeyConfigurationKey];
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

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = FitLifeJwt.Issuer,
            ValidAudience = FitLifeJwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = FitLifeJwt.AccountIdClaim,
            RoleClaimType = FitLifeJwt.RoleClaim,
        };
    }
}
