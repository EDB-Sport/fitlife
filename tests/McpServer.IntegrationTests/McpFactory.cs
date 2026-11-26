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
    /// <summary>The transport an MCP client connects to the server's <c>/mcp</c> endpoint with, over HTTP like a member's AI assistant.</summary>
    /// <param name="http">The test server's client; the caller disposes it.</param>
    /// <returns>The transport; the caller disposes it.</returns>
    public static HttpClientTransport TransportFor(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);

        // ! WebApplicationFactory.CreateClient() always sets the base address
        Uri endpoint = new(http.BaseAddress!, "mcp");
        return new HttpClientTransport(new HttpClientTransportOptions { Endpoint = endpoint }, http, ownsHttpClient: false);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (booking is not null)
        {
            builder.ConfigureServices(services => services.AddSingleton(booking));
        }
    }
}
