using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MembershipService.IntegrationTests;

/// <summary>
/// Membership is closed by default (#368): an endpoint with no authorization metadata needs a token,
/// so a new endpoint can't be left open by mistake.
/// </summary>
public sealed class ClosedByDefaultTests
{
    private const string NewEndpoint = "/test/endpoint-without-authorization";

    [Test]
    public async Task NewEndpoint_WithoutAuthorization_IsClosedByDefault()
    {
        // Arrange
        using var membership = new MembershipFactory();
        using WebApplicationFactory<Program> factory = WithNewEndpoint(membership);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri(NewEndpoint, UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task NewEndpoint_WithAToken_IsAnswered()
    {
        // Arrange
        using var membership = new MembershipFactory();
        using WebApplicationFactory<Program> factory = WithNewEndpoint(membership);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(TestTokens.NewAccountId()));

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri(NewEndpoint, UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// The app with one more endpoint that has no authorization metadata, as a new endpoint would be.
    /// A startup filter maps it ahead of the app's own pipeline, with the app's own authorization services.
    /// </summary>
    private static WebApplicationFactory<Program> WithNewEndpoint(MembershipFactory membership)
    {
        return membership.WithWebHostBuilder(AddNewEndpoint);
    }

    private static void AddNewEndpoint(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(AddNewEndpointFilter);
    }

    private static void AddNewEndpointFilter(IServiceCollection services)
    {
        services.AddSingleton<IStartupFilter>(new NewEndpointFilter());
    }

    private sealed class NewEndpointFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(MapNewEndpoint);
                next(app);
            };
        }

        private static void MapNewEndpoint(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet(NewEndpoint, Answer);
        }

        private static IResult Answer()
        {
            return Results.Ok();
        }
    }
}
