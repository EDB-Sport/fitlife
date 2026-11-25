using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FitLife.Authentication.Tests;

/// <summary>
/// A request with a token from Identity, authenticated by the JwtBearer scheme the library configures, the way a service
/// reads it: <c>User.FindFirst("sub")</c> and <c>User.IsInRole("Medlem")</c> (#183 AC 4). It runs the real
/// <see cref="JwtBearerHandler"/> on an <see cref="HttpContext"/>, without a web host, so it stays a unit test.
/// </summary>
public sealed class JwtBearerSchemeTests
{
    private const string SigningKey = "test-signing-key-for-unit-tests-only-32+";

    [Test]
    public async Task Authenticate_RequestWithATokenFromIdentity_KeepsTheAccountIdAsSub()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        HttpContext request = RequestWithToken(provider, Sign());

        // Act
        AuthenticateResult result = await request.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        Assert.That(result.Principal?.FindFirst(FitLifeJwt.AccountIdClaim)?.Value, Is.EqualTo("account-1"), result.Failure?.Message);
    }

    [Test]
    public async Task Authenticate_RequestWithATokenFromIdentity_IsInTheRoleFromTheRoleClaim()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        HttpContext request = RequestWithToken(provider, Sign());

        // Act
        AuthenticateResult result = await request.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        Assert.That(result.Principal?.IsInRole("Medlem"), Is.True, result.Failure?.Message);
    }

    private static ServiceProvider BuildProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Jwt:SigningKey"] = SigningKey })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddFitLifeJwtAuthentication();
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext RequestWithToken(IServiceProvider provider, string token)
    {
        var request = new DefaultHttpContext { RequestServices = provider };
        request.Request.Headers.Authorization = $"Bearer {token}";
        return request;
    }

    /// <summary>Signs a token the way Identity does (#169), with a test key: <c>sub</c> and <c>role</c>, as JWT names them.</summary>
    private static string Sign()
    {
        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "fitlife-identity",
            Audience = "fitlife",
            Subject = new ClaimsIdentity([new Claim("sub", "account-1"), new Claim("role", "Medlem")]),
            NotBefore = now.AddMinutes(-1),
            IssuedAt = now.AddMinutes(-1),
            Expires = now.AddMinutes(30),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
