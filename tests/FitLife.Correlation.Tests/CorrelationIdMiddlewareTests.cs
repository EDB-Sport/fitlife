using Microsoft.AspNetCore.Http;

namespace FitLife.Correlation.Tests;

public sealed class CorrelationIdMiddlewareTests
{
    private static async Task<string?> InvokeAndCaptureAsync(HttpContext context, ScopeCapturingLogger<CorrelationIdMiddleware> logger)
    {
        string? seen = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            seen = CorrelationContext.Current;
            return Task.CompletedTask;
        }, logger);

        await middleware.InvokeAsync(context).ConfigureAwait(false);
        return seen;
    }

    [Test]
    public async Task Invoke_WithHeader_UsesTheIdForTheRequest()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationContext.HeaderName] = "abc-123";

        // Act
        var seen = await InvokeAndCaptureAsync(context, new ScopeCapturingLogger<CorrelationIdMiddleware>()).ConfigureAwait(false);

        // Assert
        Assert.That(seen, Is.EqualTo("abc-123"));
    }

    [Test]
    public async Task Invoke_WithoutHeader_CreatesAnId()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var seen = await InvokeAndCaptureAsync(context, new ScopeCapturingLogger<CorrelationIdMiddleware>()).ConfigureAwait(false);

        // Assert
        Assert.That(seen, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Invoke_PutsTheIdInTheLoggingScope()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationContext.HeaderName] = "abc-123";
        var logger = new ScopeCapturingLogger<CorrelationIdMiddleware>();

        // Act
        await InvokeAndCaptureAsync(context, logger).ConfigureAwait(false);

        // Assert
        var scope = logger.Scopes.OfType<IEnumerable<KeyValuePair<string, object?>>>().Single();
        Assert.That(scope, Has.Member(new KeyValuePair<string, object?>(CorrelationContext.ScopeKey, "abc-123")));
    }

    [Test]
    public async Task Invoke_AfterTheRequest_ClearsTheId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationContext.HeaderName] = "abc-123";

        // Act
        await InvokeAndCaptureAsync(context, new ScopeCapturingLogger<CorrelationIdMiddleware>()).ConfigureAwait(false);

        // Assert
        Assert.That(CorrelationContext.Current, Is.Null);
    }
}
