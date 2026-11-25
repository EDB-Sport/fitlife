using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FitLife.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.IntegrationTests.Accounts;

/// <summary>POST /tokens (#235): log in with e-mail address and password, and get a JWT with the account id and the roles.</summary>
public sealed class TokensApiTests
{
    private const string Email = "anna@fitlife.example";
    private const string Password = "made-up-password-1";

    private static readonly Uri Accounts = new("/accounts", UriKind.Relative);
    private static readonly Uri Tokens = new("/tokens", UriKind.Relative);
    private static readonly string[] OnlyMedlem = ["Medlem"];

    // Half an hour after the app's fixed clock (IdentityApiFactory.Time): inside the token's lifetime
    private static readonly DateTime CheckedAt = new(2026, 11, 25, 12, 30, 0, DateTimeKind.Utc);

    private static async Task<HttpClient> ClientWithAnAccountAsync(IdentityApiFactory factory)
    {
        HttpClient client = factory.CreateClient();
        using HttpResponseMessage created = await client.PostAsJsonAsync(Accounts, new { email = Email, password = Password }).ConfigureAwait(false);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return client;
    }

    private static async Task<string> AccessTokenAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(Tokens, new { email = Email, password = Password }).ConfigureAwait(false);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        return body.RootElement.GetProperty("accessToken").GetString() ?? string.Empty;
    }

    // The validation parameters every other service gets from AddFitLifeJwtAuthentication() (#183)
    private static TokenValidationParameters SharedValidation(string signingKey)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [FitLifeJwt.SigningKeyConfigurationKey] = signingKey })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddFitLifeJwtAuthentication();
        using ServiceProvider provider = services.BuildServiceProvider();
        TokenValidationParameters parameters = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters.Clone();

        // The app's clock is fixed, so check the lifetime against it, not against the machine's clock
        parameters.LifetimeValidator = (notBefore, expires, _, _) => notBefore <= CheckedAt && expires > CheckedAt;
        return parameters;
    }

    [TestCase(Email)]
    [TestCase("ANNA@FitLife.Example")]
    public async Task PostTokens_WithTheRightPassword_Returns200(string email)
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = await ClientWithAnAccountAsync(factory).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(Tokens, new { email, password = Password }).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        Assert.That(body.RootElement.GetProperty("accessToken").GetString(), Is.Not.Empty);
        Assert.That(body.RootElement.TryGetProperty("expiresAt", out _), Is.True);
    }

    [Test]
    public async Task PostTokens_Token_HasTheAccountIdAndARoleClaimPerRole()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = await ClientWithAnAccountAsync(factory).ConfigureAwait(false);

        // Act
        var token = new JsonWebToken(await AccessTokenAsync(client).ConfigureAwait(false));

        // Assert
        Guid accountId = factory.Repository.Accounts.Single().Id;
        Assert.That(token.GetClaim(FitLifeJwt.AccountIdClaim).Value, Is.EqualTo(accountId.ToString()));
        Assert.That(token.Claims.Where(claim => claim.Type == FitLifeJwt.RoleClaim).Select(claim => claim.Value), Is.EqualTo(OnlyMedlem));
        Assert.That(token.Claims.Any(claim => claim.Type.Contains("member", StringComparison.OrdinalIgnoreCase)), Is.False);
    }

    [Test]
    public async Task PostTokens_Token_ExpiresAfter60Minutes()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = await ClientWithAnAccountAsync(factory).ConfigureAwait(false);
        DateTimeOffset now = factory.Time.GetUtcNow();

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(Tokens, new { email = Email, password = Password }).ConfigureAwait(false);

        // Assert
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        var token = new JsonWebToken(body.RootElement.GetProperty("accessToken").GetString());
        Assert.That(body.RootElement.GetProperty("expiresAt").GetDateTimeOffset(), Is.EqualTo(now.AddMinutes(60)));
        Assert.That(token.ValidTo, Is.EqualTo(now.AddMinutes(60).UtcDateTime));
    }

    [Test]
    public async Task PostTokens_Token_IsAcceptedBySharedValidation()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = await ClientWithAnAccountAsync(factory).ConfigureAwait(false);
        string token = await AccessTokenAsync(client).ConfigureAwait(false);

        // Act
        TokenValidationResult result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token, SharedValidation(IdentityApiFactory.SigningKey)).ConfigureAwait(false);

        // Assert
        Assert.That(result.IsValid, Is.True, result.Exception?.Message);
    }

    [Test]
    public async Task PostTokens_Token_IsRejectedWithAnotherKey()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = await ClientWithAnAccountAsync(factory).ConfigureAwait(false);
        string token = await AccessTokenAsync(client).ConfigureAwait(false);

        // Act
        TokenValidationResult result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token, SharedValidation("another-signing-key-that-identity-never-used")).ConfigureAwait(false);

        // Assert
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task PostTokens_WithAnUnknownEmailOrAWrongPassword_ReturnsTheSame401()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = await ClientWithAnAccountAsync(factory).ConfigureAwait(false);

        // Act
        using HttpResponseMessage unknownEmail = await client.PostAsJsonAsync(Tokens, new { email = "bo@fitlife.example", password = Password }).ConfigureAwait(false);
        using HttpResponseMessage wrongPassword = await client.PostAsJsonAsync(Tokens, new { email = Email, password = "the-wrong-password" }).ConfigureAwait(false);

        // Assert
        Assert.That(unknownEmail.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(wrongPassword.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(unknownEmail.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        string unknownEmailBody = await unknownEmail.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.That(await wrongPassword.Content.ReadAsStringAsync().ConfigureAwait(false), Is.EqualTo(unknownEmailBody));
    }

    [Test]
    public async Task PostTokens_LogLines_HaveNoPasswordOrToken()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = await ClientWithAnAccountAsync(factory).ConfigureAwait(false);

        // Act
        string token = await AccessTokenAsync(client).ConfigureAwait(false);

        // Assert
        Assert.That(factory.Logs.Lines, Has.None.Contains(Password));
        Assert.That(factory.Logs.Lines, Has.None.Contains(token));
    }
}
