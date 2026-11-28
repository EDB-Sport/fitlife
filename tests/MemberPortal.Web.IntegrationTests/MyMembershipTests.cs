using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace MemberPortal.Web.IntegrationTests;

/// <summary><c>/mit-medlemskab</c> needs a log-in (#624), through the real pipeline.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class MyMembershipTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public MyMembershipTests()
    {
        // No auto-redirect, so the test sees the redirect
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task MyMembership_WithoutLogIn_RedirectsToLogInd()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(new Uri("/mit-medlemskab", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(PortalForms.RedirectPath(_client, response), Is.EqualTo("/log-ind"));
    }
}
