using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace IdentityService.Tests.Application;

/// <summary>Identity writes the token with the FitLifeJwt constants (#183), so writer and checker can't drift (#235).</summary>
public sealed class TokenWriterTests
{
    private const string SigningKey = "test-signing-key-for-unit-tests-only-32+";
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private static TokenWriter Writer(string? signingKey)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Jwt:SigningKey"] = signingKey })
            .Build();
        return new TokenWriter(configuration, new FakeTimeProvider(Now));
    }

    private static Account AnAccount()
    {
        return Account.Create("anna@fitlife.example", "AQAAAAIAAYagAAAAEhash");
    }

    [Test]
    public void Write_Token_HasTheIssuerTheAudienceTheAccountIdAndTheRoles()
    {
        // Arrange
        Account account = AnAccount();

        // Act
        LogInResult result = Writer(SigningKey).Write(account);

        // Assert
        var token = new JsonWebToken(result.AccessToken);
        Assert.That(token.Issuer, Is.EqualTo("fitlife-identity"));
        Assert.That(token.Audiences, Is.EqualTo(new[] { "fitlife" }));
        Assert.That(token.Subject, Is.EqualTo(account.Id.ToString()));
        Assert.That(token.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value), Is.EqualTo(new[] { "Medlem" }));
        Assert.That(token.Alg, Is.EqualTo("HS256"));
    }

    [Test]
    public void Write_Token_IsValidFromNowAndExpiresAfter60Minutes()
    {
        // Arrange & Act
        LogInResult result = Writer(SigningKey).Write(AnAccount());

        // Assert
        var token = new JsonWebToken(result.AccessToken);
        Assert.That(result.ExpiresAt, Is.EqualTo(Now.AddMinutes(60)));
        Assert.That(token.ValidTo, Is.EqualTo(Now.AddMinutes(60).UtcDateTime));
        Assert.That(token.ValidFrom, Is.EqualTo(Now.UtcDateTime));
        Assert.That(token.IssuedAt, Is.EqualTo(Now.UtcDateTime));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Write_WithoutASigningKey_ThrowsAndNamesTheSetting(string? signingKey)
    {
        // Arrange
        TokenWriter writer = Writer(signingKey);

        // Act & Assert
        Assert.That(() => writer.Write(AnAccount()), Throws.InvalidOperationException.With.Message.Contains("Jwt:SigningKey"));
    }

    [Test]
    public void Write_WithAShortSigningKey_ThrowsWithoutShowingTheKey()
    {
        // Arrange
        const string shortKey = "a-key-of-exactly-31-bytes-long!";
        TokenWriter writer = Writer(shortKey);

        // Act & Assert
        Assert.That(() => writer.Write(AnAccount()), Throws.InvalidOperationException
            .With.Message.Contains("Jwt:SigningKey")
            .And.Message.Not.Contains(shortKey));
    }

    [Test]
    public void Write_WithASigningKeyOf32Bytes_WritesAToken()
    {
        // Arrange & Act
        LogInResult result = Writer("a-key-of-exactly-32-bytes-long!!").Write(AnAccount());

        // Assert
        Assert.That(result.AccessToken, Is.Not.Empty);
    }
}
