using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpServer.Guardrails;

/// <summary>
/// One <c>Information</c> line per tool call, with the caller's account id and the tool's name (#216). It is written in the request's
/// correlation ID scope, and holds no token and no tool input.
/// </summary>
public static partial class ToolCallLog
{
    /// <summary>The account id in the line when the request has no readable token.</summary>
    public const string NoAccount = "none";

    /// <summary>The log category of the call log.</summary>
    public const string Category = "McpServer.Guardrails.ToolCallLog";

    /// <summary>Wraps the tool call handler so each call is logged before it runs.</summary>
    /// <param name="next">The handler that runs the tool.</param>
    /// <returns>The handler that logs and then runs the tool.</returns>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Around(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (request, cancellationToken) =>
        {
            ILogger logger = request.Services?.GetService<ILoggerFactory>()?.CreateLogger(Category) ?? NullLogger.Instance;
            if (logger.IsEnabled(LogLevel.Information))
            {
                string? header = request.Services?.GetService<IHttpContextAccessor>()?.HttpContext?.Request.Headers.Authorization;
                string accountId = CallerAccount.AccountIdFrom(header) ?? NoAccount;
                LogToolCall(logger, accountId, request.Params?.Name);
            }

            return await next(request, cancellationToken).ConfigureAwait(false);
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Tool call by account {AccountId}: {ToolName}")]
    private static partial void LogToolCall(ILogger logger, string accountId, string? toolName);
}
