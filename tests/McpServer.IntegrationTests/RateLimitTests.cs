using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace McpServer.IntegrationTests;

/// <summary>
/// At most <c>Mcp:CallsPerMinute</c> requests to <c>/mcp</c> per account and minute (#216), so an AI assistant can't flood Booking.
/// The tests set a small limit and stay well inside the minute.
/// </summary>
public sealed class RateLimitTests
{
    [Test]
    public async Task McpRequest_OverTheLimit_Gets429()
    {
        // Arrange
        using var app = new McpFactory();
        using WebApplicationFactory<Program> factory = WithLimit(app, 2);
        using HttpClient client = factory.CreateClient();
        string token = TokenFor("account-a");
        using HttpResponseMessage first = await PingAsync(client, token).ConfigureAwait(false);
        using HttpResponseMessage second = await PingAsync(client, token).ConfigureAwait(false);

        // Act
        using HttpResponseMessage third = await PingAsync(client, token).ConfigureAwait(false);

        // Assert
        Assert.That(new[] { first.StatusCode, second.StatusCode, third.StatusCode }, Is.EqualTo(new[] { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests }));
    }

    [Test]
    public async Task McpRequest_FromAnotherAccount_HasItsOwnLimit()
    {
        // Arrange: account A has used its one request
        using var app = new McpFactory();
        using WebApplicationFactory<Program> factory = WithLimit(app, 1);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage fromA = await PingAsync(client, TokenFor("account-a")).ConfigureAwait(false);

        // Act
        using HttpResponseMessage fromB = await PingAsync(client, TokenFor("account-b")).ConfigureAwait(false);

        // Assert
        Assert.That(fromB.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task McpRequest_WithoutAToken_SharesOneLimit()
    {
        // Arrange: a request without a token uses the one request; a token that can't be read counts as none
        using var app = new McpFactory();
        using WebApplicationFactory<Program> factory = WithLimit(app, 1);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage withoutAToken = await PingAsync(client, token: null).ConfigureAwait(false);

        // Act
        using HttpResponseMessage withAnUnreadableToken = await PingAsync(client, "not-a-token").ConfigureAwait(false);

        // Assert
        Assert.That(withAnUnreadableToken.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
    }

    [Test]
    public async Task Health_OverTheLimit_IsNotLimited()
    {
        // Arrange
        using var app = new McpFactory();
        using WebApplicationFactory<Program> factory = WithLimit(app, 1);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage first = await client.GetAsync(new Uri("/health", UriKind.Relative)).ConfigureAwait(false);

        // Act
        using HttpResponseMessage second = await client.GetAsync(new Uri("/health", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static WebApplicationFactory<Program> WithLimit(McpFactory app, int callsPerMinute)
    {
        return app.WithWebHostBuilder(host => host.UseSetting("Mcp:CallsPerMinute", callsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>An MCP ping (JSON-RPC), the smallest request <c>/mcp</c> answers 200 without a session.</summary>
    private static async Task<HttpResponseMessage> PingAsync(HttpClient client, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/mcp", UriKind.Relative))
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"ping"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static string TokenFor(string accountId)
    {
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Claims = new Dictionary<string, object>(StringComparer.Ordinal) { ["sub"] = accountId } });
    }
}
