using McpServer.Booking;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;

namespace McpServer.IntegrationTests;

/// <summary>
/// Starts the MCP server, with <see cref="IBookingClient"/> replaced when the test gives one: the gateway and Booking aren't running.
/// Without one, nothing asks Booking, and the server starts with no <c>Gateway:BaseUrl</c> because the client reads it only when it is made.
/// </summary>
internal sealed class McpFactory(IBookingClient? booking = null) : WebApplicationFactory<Program>
{
    /// <summary>Connects an MCP client to the server's <c>/mcp</c> endpoint, over HTTP like a member's AI assistant.</summary>
    /// <param name="http">The test server's client; the caller disposes it.</param>
    /// <returns>The connected client; dispose it.</returns>
    public static async Task<McpClient> ConnectAsync(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "mcp") },
            http,
            ownsHttpClient: false);
        return await McpClient.CreateAsync(transport).ConfigureAwait(false);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (booking is not null)
        {
            builder.ConfigureServices(services => services.AddSingleton(booking));
        }
    }
}
