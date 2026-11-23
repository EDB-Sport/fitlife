namespace FitLife.Correlation.Tests;

public sealed class CorrelationIdHandlerTests
{
    private static async Task<HttpRequestMessage> SendAndCaptureAsync()
    {
        var inner = new CapturingHandler();
        using var handler = new CorrelationIdHandler { InnerHandler = inner };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("http://membership-svc/members/1"));

        using var response = await invoker.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
        return inner.Request ?? throw new InvalidOperationException("The request never reached the inner handler.");
    }

    [Test]
    public async Task Send_DuringARequestWithAnId_ForwardsTheHeader()
    {
        // Arrange
        using var scope = CorrelationContext.Begin(new ScopeCapturingLogger<CorrelationIdHandlerTests>(), "abc-123");

        // Act
        var sent = await SendAndCaptureAsync().ConfigureAwait(false);

        // Assert
        Assert.That(sent.Headers.GetValues(CorrelationContext.HeaderName), Is.EqualTo(new List<string> { "abc-123" }));
    }

    [Test]
    public async Task Send_WithoutAnId_AddsNoHeader()
    {
        // Arrange: no CorrelationContext.Begin

        // Act
        var sent = await SendAndCaptureAsync().ConfigureAwait(false);

        // Assert
        Assert.That(sent.Headers.Contains(CorrelationContext.HeaderName), Is.False);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
