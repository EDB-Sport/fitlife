using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FitLife.Correlation;

/// <summary>
/// Reads the correlation ID from the request's <c>X-Correlation-ID</c> header, or creates one, for the rest of the request.
/// </summary>
/// <param name="next">The rest of the pipeline.</param>
/// <param name="logger">The logger whose scope carries the ID.</param>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>Handles one request with its correlation ID as the current ID.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the rest of the pipeline has handled the request.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string? header = context.Request.Headers[CorrelationContext.HeaderName];
        string id;
        if (string.IsNullOrWhiteSpace(header))
        {
            id = Guid.NewGuid().ToString();
        }
        else
        {
            id = header;
        }

        using (CorrelationContext.Begin(logger, id))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
