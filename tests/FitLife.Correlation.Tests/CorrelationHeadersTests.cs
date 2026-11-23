using System.Text;

namespace FitLife.Correlation.Tests;

/// <summary>RabbitMQ message headers: the publisher writes the ID, the consumer reads it back.</summary>
public sealed class CorrelationHeadersTests
{
    [Test]
    public void Write_DuringARequestWithAnId_PutsTheIdInTheHeaders()
    {
        // Arrange
        using var scope = CorrelationContext.Begin(new ScopeCapturingLogger<CorrelationHeadersTests>(), "abc-123");
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal);

        // Act
        CorrelationHeaders.Write(headers);

        // Assert
        Assert.That(headers[CorrelationContext.HeaderName], Is.EqualTo("abc-123"));
    }

    [Test]
    public void Read_BytesAsRabbitMqDeliversThem_ReturnsTheId()
    {
        // Arrange
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [CorrelationContext.HeaderName] = Encoding.UTF8.GetBytes("abc-123"),
        };

        // Act
        var id = CorrelationHeaders.Read(headers);

        // Assert
        Assert.That(id, Is.EqualTo("abc-123"));
    }

    [Test]
    public void Read_WithoutTheHeader_ReturnsNull()
    {
        // Arrange
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal);

        // Act
        var id = CorrelationHeaders.Read(headers);

        // Assert
        Assert.That(id, Is.Null);
    }
}
