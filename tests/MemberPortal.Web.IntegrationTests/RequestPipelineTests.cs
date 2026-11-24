using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace MemberPortal.Web.IntegrationTests;

/// <summary>
/// The request pipeline in Program.cs, one behaviour per test: pages, HTTPS, HSTS, the error page,
/// static files and the Interactive Server endpoint.
/// </summary>
[TestFixture]
public sealed class RequestPipelineTests
{
    private const string FailingPath = "/test-only/throws";

    [Test]
    public async Task HomePage_InProduction_RendersTheFitLifeHeading()
    {
        using PortalFactory factory = Create("Production");
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative)).ConfigureAwait(false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.That(content, Does.Contain("Velkommen til FitLife Digital"));
    }

    [Test]
    public async Task HttpRequest_WithAnHttpsPort_IsRedirectedToHttps()
    {
        using PortalFactory factory = Create("Production", host => host.UseSetting("https_port", "443"));
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative)).ConfigureAwait(false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TemporaryRedirect));
        Assert.That(response.Headers.Location?.Scheme, Is.EqualTo(Uri.UriSchemeHttps));
    }

    [Test]
    public async Task HttpsRequest_InProduction_SetsHsts()
    {
        using PortalFactory factory = Create("Production");
        using HttpClient client = CreateHttpsClient(factory);

        using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative)).ConfigureAwait(false);

        Assert.That(response.Headers.Contains("Strict-Transport-Security"), Is.True);
    }

    [Test]
    public async Task HttpsRequest_InDevelopment_DoesNotSetHsts()
    {
        using PortalFactory factory = Create("Development");
        using HttpClient client = CreateHttpsClient(factory);

        using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative)).ConfigureAwait(false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.Contains("Strict-Transport-Security"), Is.False);
    }

    [Test]
    public async Task UnhandledException_InProduction_ShowsTheErrorPage()
    {
        using PortalFactory factory = Create("Production", AddThrowingPath);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri(FailingPath, UriKind.Relative)).ConfigureAwait(false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.That(content, Does.Contain("An error occurred while processing your request."));
    }

    [Test]
    public async Task StaticFile_IsServed()
    {
        using PortalFactory factory = Create("Development");
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/app.css", UriKind.Relative)).ConfigureAwait(false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/css"));
    }

    [Test]
    public async Task InteractiveServerHub_AcceptsANegotiateRequest()
    {
        using PortalFactory factory = Create("Development");
        using HttpClient client = factory.CreateClient();
        using HttpContent empty = new StringContent(string.Empty);

        using HttpResponseMessage response = await client.PostAsync(new Uri("/_blazor/negotiate?negotiateVersion=1", UriKind.Relative), empty).ConfigureAwait(false);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static PortalFactory Create(string environment, Action<IWebHostBuilder>? configure = null)
    {
        return new PortalFactory(environment, configure);
    }

    private static void AddThrowingPath(IWebHostBuilder host)
    {
        host.ConfigureServices(services => services.AddSingleton<IStartupFilter>(new ThrowingPathFilter()));
    }

    // HSTS skips localhost, so the request goes to another host name over HTTPS
    private static HttpClient CreateHttpsClient(PortalFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://portal.fitlife.test") });
    }

    private sealed class PortalFactory(string environment, Action<IWebHostBuilder>? configure) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            configure?.Invoke(builder);
        }
    }

    // Runs after the app's own pipeline, so the exception passes through UseExceptionHandler like any other
    private sealed class ThrowingPathFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                next(app);
                app.Use((HttpContext context, RequestDelegate _) =>
                {
                    if (context.Request.Path == FailingPath)
                    {
                        throw new InvalidOperationException("Thrown on purpose by the test.");
                    }

                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return Task.CompletedTask;
                });
            };
        }
    }
}
