using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FitLife.Authentication.Tests;

/// <summary>
/// Every service checks roles the same way, and an endpoint is closed until it is opened on purpose (#362).
/// The requests run through ASP.NET Core's own authentication and authorization middleware on a
/// <see cref="DefaultHttpContext"/>, with no web host (FITLIFE002).
/// </summary>
public sealed class AuthorizationTests
{
    private const string SigningKey = "test-signing-key-for-unit-tests-only-32+";

    [Test]
    public async Task Fallback_WithoutAToken_Returns401()
    {
        // Arrange
        await using ServiceProvider services = BuildServices();

        // Act
        int status = await Send(services, token: null);

        // Assert
        Assert.That(status, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task Fallback_WithAToken_IsAnswered()
    {
        // Arrange
        await using ServiceProvider services = BuildServices();

        // Act
        int status = await Send(services, Sign(FitLifeRoles.Medlem));

        // Assert
        Assert.That(status, Is.EqualTo(StatusCodes.Status200OK));
    }

    [Test]
    public async Task AllowAnonymous_WithoutAToken_IsAnswered()
    {
        // Arrange
        await using ServiceProvider services = BuildServices();

        // Act
        int status = await Send(services, token: null, new AllowAnonymousAttribute());

        // Assert
        Assert.That(status, Is.EqualTo(StatusCodes.Status200OK));
    }

    [TestCase(FitLifeRoles.Medlem, StatusCodes.Status200OK)]
    [TestCase(FitLifeRoles.Traener, StatusCodes.Status403Forbidden)]
    [TestCase(FitLifeRoles.Admin, StatusCodes.Status403Forbidden)]
    public async Task Medlem_WithEachRole_LetsInMedlemOnly(string role, int expected)
    {
        // Arrange
        await using ServiceProvider services = BuildServices();

        // Act
        int status = await Send(services, Sign(role), new AuthorizeAttribute(FitLifePolicies.Medlem));

        // Assert
        Assert.That(status, Is.EqualTo(expected));
    }

    [TestCase(FitLifeRoles.Medlem, StatusCodes.Status403Forbidden)]
    [TestCase(FitLifeRoles.Traener, StatusCodes.Status200OK)]
    [TestCase(FitLifeRoles.Admin, StatusCodes.Status200OK)]
    public async Task TraenerOrAdmin_WithEachRole_LetsInTraenerAndAdmin(string role, int expected)
    {
        // Arrange
        await using ServiceProvider services = BuildServices();

        // Act
        int status = await Send(services, Sign(role), new AuthorizeAttribute(FitLifePolicies.TraenerOrAdmin));

        // Assert
        Assert.That(status, Is.EqualTo(expected));
    }

    [TestCase(FitLifeRoles.Medlem, StatusCodes.Status403Forbidden)]
    [TestCase(FitLifeRoles.Traener, StatusCodes.Status403Forbidden)]
    [TestCase(FitLifeRoles.Admin, StatusCodes.Status200OK)]
    public async Task Admin_WithEachRole_LetsInAdminOnly(string role, int expected)
    {
        // Arrange
        await using ServiceProvider services = BuildServices();

        // Act
        int status = await Send(services, Sign(role), new AuthorizeAttribute(FitLifePolicies.Admin));

        // Assert
        Assert.That(status, Is.EqualTo(expected));
    }

    [TestCase(FitLifePolicies.Medlem)]
    [TestCase(FitLifePolicies.TraenerOrAdmin)]
    [TestCase(FitLifePolicies.Admin)]
    public async Task Policy_WithoutAToken_Returns401(string policy)
    {
        // Arrange
        await using ServiceProvider services = BuildServices();

        // Act
        int status = await Send(services, token: null, new AuthorizeAttribute(policy));

        // Assert
        Assert.That(status, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public void Roles_AreTheValuesOfTheRoleClaim()
    {
        // Assert: GLOSSARY, Rolle. "Træner" keeps its æ
        using (Assert.EnterMultipleScope())
        {
            Assert.That(FitLifeRoles.Medlem, Is.EqualTo("Medlem"));
            Assert.That(FitLifeRoles.Traener, Is.EqualTo("Træner"));
            Assert.That(FitLifeRoles.Admin, Is.EqualTo("Admin"));
        }
    }

    private static ServiceProvider BuildServices()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [FitLifeJwt.SigningKeyConfigurationKey] = SigningKey })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddRouting();
        services.AddFitLifeJwtAuthentication();
        services.AddFitLifeAuthorization();
        return services.BuildServiceProvider();
    }

    /// <summary>Sends one request to an endpoint with the given metadata, through authentication and authorization.</summary>
    private static async Task<int> Send(ServiceProvider services, string? token, params object[] metadata)
    {
        using IServiceScope scope = services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        if (token is not null)
        {
            context.Request.Headers.Authorization = $"Bearer {token}";
        }

        RequestDelegate endpoint = answered =>
        {
            answered.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };
        context.SetEndpoint(new Endpoint(endpoint, new EndpointMetadataCollection(metadata), "test endpoint"));

        var authorization = ActivatorUtilities.CreateInstance<AuthorizationMiddleware>(services, endpoint);
        var authentication = ActivatorUtilities.CreateInstance<AuthenticationMiddleware>(services, (RequestDelegate)authorization.Invoke);
        await authentication.Invoke(context).ConfigureAwait(false);
        return context.Response.StatusCode;
    }

    /// <summary>Signs a token the way Identity does (#169), for account-1 with one role.</summary>
    private static string Sign(string role)
    {
        DateTime now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = FitLifeJwt.Issuer,
            Audience = FitLifeJwt.Audience,
            Subject = new ClaimsIdentity([new Claim(FitLifeJwt.AccountIdClaim, "account-1"), new Claim(FitLifeJwt.RoleClaim, role)]),
            NotBefore = now.AddMinutes(-1),
            IssuedAt = now.AddMinutes(-1),
            Expires = now.AddMinutes(30),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
