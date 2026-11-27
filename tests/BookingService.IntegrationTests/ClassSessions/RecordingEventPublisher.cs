using System.Collections.Concurrent;
using FitLife.Messaging;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>Keeps what Booking publishes, so the HTTP tests need no RabbitMQ (#192). The flow tests use the real one.</summary>
internal sealed class RecordingEventPublisher : IEventPublisher
{
    private readonly ConcurrentQueue<object> _published = new();

    /// <summary>The published events, in order.</summary>
    public IReadOnlyList<object> Published => _published.ToArray();

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        _published.Enqueue(integrationEvent);
        return Task.CompletedTask;
    }
}
