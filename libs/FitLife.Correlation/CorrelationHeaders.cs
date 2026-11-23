using System.Text;

namespace FitLife.Correlation;

/// <summary>
/// The correlation ID in RabbitMQ message headers. The publisher calls <see cref="Write"/>; the consumer calls
/// <see cref="Read"/> and handles the message inside <see cref="CorrelationContext.Begin"/>.
/// </summary>
public static class CorrelationHeaders
{
    /// <summary>Puts the current correlation ID, if there is one, in a message's headers.</summary>
    /// <param name="headers">The message headers (<c>IBasicProperties.Headers</c>).</param>
    public static void Write(IDictionary<string, object?> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var id = CorrelationContext.Current;
        if (id is not null)
        {
            headers[CorrelationContext.HeaderName] = id;
        }
    }

    /// <summary>Reads the correlation ID from a received message's headers.</summary>
    /// <param name="headers">The message headers. RabbitMQ delivers string headers as UTF-8 bytes.</param>
    /// <returns>The ID, or null if the message has none.</returns>
    public static string? Read(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(CorrelationContext.HeaderName, out var value))
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => null,
        };
    }
}
