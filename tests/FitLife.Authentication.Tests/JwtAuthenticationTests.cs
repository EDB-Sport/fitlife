using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FitLife.Authentication.Tests;

/// <summary>Every service checks the token from Identity the same way (CONTEXT-MAP section 4, #183).</summary>
public sealed class JwtAuthenticationTests
{
    private const string SigningKey = "test-signing-key-for-unit-tests-only-32+";
    private const string OtherKey = "another-signing-key-that-identity-never-used";

    // HS512 needs a key of at least 64 bytes, so the HS512 test uses this one on both sides
    private const string LongKey = "a-test-signing-key-long-enough-for-hmac-sha512-as-well-as-hs256-0123456789";

    [Test]
    public async Task Validate_TokenFromIdentity_IsValid()
    {
        // Arrange
        TokenValidationParameters parameters = ParametersFor(SigningKey);
        string token = Sign(SigningKey);

        // Act
        TokenValidationResult result = await Validate(token, parameters);

        // Assert
        Assert.That(result.IsValid, Is.True, result.Exception?.Message);
    }

    [Test]
    public async Task Validate_TokenFromIdentity_CarriesTheAccountIdClaim()
    {
        // Arrange
        TokenValidationParameters parameters = ParametersFor(SigningKey);
        string token = Sign(SigningKey);

        // Act
        TokenValidationResult result = await Validate(token, parameters);

        // Assert
        Assert.That(result.ClaimsIdentity.FindFirst(FitLifeJwt.AccountIdClaim)?.Value, Is.EqualTo("account-1"));
    }

    [Test]
    public async Task Validate_TokenSignedWithAnotherKey_IsRejected()
    {
        // Arrange
        TokenValidationParameters parameters = ParametersFor(SigningKey);
        string token = Sign(OtherKey);

        // Act
        TokenValidationResult result = await Validate(token, parameters);

        // Assert
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task Validate_TokenFromAnotherIssuer_IsRejected()
    {
        // Arrange
        TokenValidationParameters parameters = ParametersFor(SigningKey);
        string token = Sign(SigningKey, issuer: "someone-else");

        // Act
        TokenValidationResult result = await Validate(token, parameters);

        // Assert
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task Validate_TokenForAnotherAudience_IsRejected()
    {
        // Arrange
        TokenValidationParameters parameters = ParametersFor(SigningKey);
        string token = Sign(SigningKey, audience: "another-system");

        // Act
        TokenValidationResult result = await Validate(token, parameters);

        // Assert
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task Validate_ExpiredToken_IsRejected()
    {
        // Arrange
        TokenValidationParameters parameters = ParametersFor(SigningKey);
        string token = Sign(SigningKey, expires: new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        // Act
        TokenValidationResult result = await Validate(token, parameters);

        // Assert
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task Validate_TokenSignedWithHs512_IsRejected()
    {
        // Arrange: same key, another algorithm. Only HS256 is accepted
        TokenValidationParameters parameters = ParametersFor(LongKey);
        string token = Sign(LongKey, algorithm: SecurityAlgorithms.HmacSha512);

        // Act
        TokenValidationResult result = await Validate(token, parameters);

        // Assert
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task Validate_TokenFromIdentity_CarriesTheRoleForIsInRole()
    {
        // Arrange
        TokenValidationParameters parameters = ParametersFor(SigningKey);
        string token = Sign(SigningKey);

        // Act
        TokenValidationResult result = await Validate(token, parameters);

        // Assert
        Assert.That(new ClaimsPrincipal(result.ClaimsIdentity).IsInRole("Medlem"), Is.True);
    }

    [Test]
    public void Configure_WithoutASigningKey_FailsAndNamesTheConfigurationKey()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider(signingKey: null);

        // Act
        Action act = () => _ = Options(provider);

        // Assert
        Assert.That(act, Throws.InvalidOperationException.With.Message.Contains("Jwt:SigningKey"));
    }

    [Test]
    public void Configure_WithAShortSigningKey_FailsWithoutShowingTheKey()
    {
        // Arrange
        const string shortKey = "too-short-key";
        using ServiceProvider provider = BuildProvider(shortKey);

        // Act
        Action act = () => _ = Options(provider);

        // Assert
        Assert.That(act, Throws.InvalidOperationException
            .With.Message.Contains("Jwt:SigningKey")
            .And.Message.Not.Contains(shortKey));
    }

    private static ServiceProvider BuildProvider(string? signingKey)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Jwt:SigningKey"] = signingKey })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddFitLifeJwtAuthentication();
        return services.BuildServiceProvider();
    }

    private static JwtBearerOptions Options(ServiceProvider provider)
    {
        return provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
    }

    private static TokenValidationParameters ParametersFor(string signingKey)
    {
        using ServiceProvider provider = BuildProvider(signingKey);
        return Options(provider).TokenValidationParameters;
    }

    private static Task<TokenValidationResult> Validate(string token, TokenValidationParameters parameters)
    {
        return new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
    }

    /// <summary>Signs a token the way Identity does (#169), with a test key.</summary>
    private static string Sign(
        string key,
        string issuer = "fitlife-identity",
        string audience = "fitlife",
        DateTime? expires = null,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        DateTime expiresAt = expires ?? now.AddMinutes(30);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([new Claim("sub", "account-1"), new Claim("role", "Medlem")]),
            NotBefore = expiresAt.AddHours(-1),
            IssuedAt = expiresAt.AddHours(-1),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), algorithm),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
