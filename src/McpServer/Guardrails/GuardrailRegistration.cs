using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpServer.Guardrails;

/// <summary>
/// The last guardrails of the ADR from #212 (#216): a rate limit per account on <c>/mcp</c>, so an AI assistant can't flood Booking,
/// and one log line per tool call, so staff can follow a call in Grafana.
/// </summary>
public static class GuardrailRegistration
{
    /// <summary>The name of the rate limit policy on <c>/mcp</c>.</summary>
    public const string RateLimitPolicy = "mcp-per-account";

    /// <summary>Registers the rate limit, read from <c>Mcp:CallsPerMinute</c>.</summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddMcpRateLimit(this IServiceCollection services)
    {
        services.AddOptions<McpOptions>().BindConfiguration(McpOptions.Section);
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(RateLimitPolicy, PartitionFor);
        });
        return services;
    }

    /// <summary>Adds the call log: one <c>Information</c> line per tool call, with the account id and the tool's name. No token, no tool input.</summary>
    /// <param name="mcp">The MCP server.</param>
    /// <returns>The MCP server.</returns>
    public static IMcpServerBuilder WithToolCallLog(this IMcpServerBuilder mcp)
    {
        ArgumentNullException.ThrowIfNull(mcp);
        mcp.Services.AddHttpContextAccessor();
        return mcp.WithRequestFilters(filters => filters.AddCallToolFilter(ToolCallLog.Around));
    }

    // One limit per account; requests without a readable token share the one of the empty key
    private static RateLimitPartition<string> PartitionFor(HttpContext context)
    {
        int callsPerMinute = context.RequestServices.GetRequiredService<IOptions<McpOptions>>().Value.CallsPerMinute;
        return RateLimitPartition.GetFixedWindowLimiter(
            CallerAccount.AccountIdFrom(context.Request.Headers.Authorization) ?? string.Empty,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = callsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    }
}
