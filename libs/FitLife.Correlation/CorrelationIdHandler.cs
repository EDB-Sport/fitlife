namespace FitLife.Correlation;

/// <summary>
/// Forwards the current correlation ID to other services. Add it to each <c>IHttpClientFactory</c> client with
/// <c>AddHttpMessageHandler&lt;CorrelationIdHandler&gt;()</c>.
/// </summary>
public sealed class CorrelationIdHandler : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? id = CorrelationContext.Current;
        if (id is not null && !request.Headers.Contains(CorrelationContext.HeaderName))
        {
            request.Headers.Add(CorrelationContext.HeaderName, id);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
