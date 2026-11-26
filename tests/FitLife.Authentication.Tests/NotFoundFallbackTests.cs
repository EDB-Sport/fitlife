using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitLife.Authentication.Tests;

/// <summary>
/// The fallback policy of AddFitLifeAuthorization() also covers a request that matches no endpoint, so an unknown
/// path answered 401 instead of 404 (#440). The requests run through the app's own routing, authentication and
/// authorization middleware, built from a <see cref="WebApplication"/> that is never started (no server, FITLIFE002).
/// </summary>
public sealed class NotFoundFallbackTests
{
    private const string SigningKey = "test-signing-key-for-unit-tests-only-32+";

    [TestCase("/no-such-path")]
    [TestCase("/openapi/v1.json")]
    public async Task UnknownPath_WithTheNotFoundFallback_Returns404(string path)
    {
        // Arrange
        await using WebApplication app = BuildApp(withNotFoundFallback: true);

        // Act
        int status = await Send(app, path);

        // Assert
        Assert.That(status, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    [Test]
    public async Task UnknownPath_WithoutTheNotFoundFallback_Returns401()
    {
        // Arrange
        await using WebApplication app = BuildApp(withNotFoundFallback: false);

        // Act
        int status = await Send(app, "/no-such-path");

        // Assert
        Assert.That(status, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task MappedEndpoint_WithTheNotFoundFallback_StillNeedsAToken()
    {
        // Arrange
        await using WebApplication app = BuildApp(withNotFoundFallback: true);

        // Act
        int status = await Send(app, "/members/me");

        // Assert
        Assert.That(status, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    private static WebApplication BuildApp(bool withNotFoundFallback)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [FitLifeJwt.SigningKeyConfigurationKey] = SigningKey });
        builder.Services.AddFitLifeJwtAuthentication();
        builder.Services.AddFitLifeAuthorization();
        WebApplication app = builder.Build();
        // A started host adds routing first and the endpoints last by itself; this app is never started
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/members/me", Answered);
        if (withNotFoundFallback)
        {
            app.MapFitLifeNotFoundFallback();
        }

        app.UseEndpoints(_ => { });
        return app;
    }

    /// <summary>Sends one request without a token through the app's middleware and endpoints.</summary>
    private static async Task<int> Send(WebApplication app, string path)
    {
        RequestDelegate pipeline = ((IApplicationBuilder)app).Build();
        using IServiceScope scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        await pipeline(context).ConfigureAwait(false);
        return context.Response.StatusCode;
    }

    private static IResult Answered()
    {
        return Results.Ok();
    }
}
