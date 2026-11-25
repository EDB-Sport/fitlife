namespace FitLife.Correlation.Tests;

/// <summary>
/// CorrelationContext itself, called synchronously. An async caller can't see a missing reset: the AsyncLocal change is undone
/// anyway when the async method returns (#326).
/// </summary>
public sealed class CorrelationContextTests
{
    [Test]
    public void Begin_ThenDispose_ClearsTheId()
    {
        // Arrange
        IDisposable correlation = CorrelationContext.Begin(new ScopeCapturingLogger<CorrelationContextTests>(), "a");

        // Act
        correlation.Dispose();

        // Assert
        Assert.That(CorrelationContext.Current, Is.Null);
    }

    [Test]
    public void Begin_InsideAnother_RestoresTheOuterIdOnDispose()
    {
        // Arrange
        var logger = new ScopeCapturingLogger<CorrelationContextTests>();
        using IDisposable outer = CorrelationContext.Begin(logger, "outer");
        IDisposable inner = CorrelationContext.Begin(logger, "inner");

        // Act
        inner.Dispose();

        // Assert
        Assert.That(CorrelationContext.Current, Is.EqualTo("outer"));
    }
}
