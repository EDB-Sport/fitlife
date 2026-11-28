using FitLife.Messaging;

namespace BookingService.Tests.Application;

/// <summary>An <see cref="IEventPublisher"/> that keeps what is published, or fails with <see cref="Failure"/> (#192).</summary>
public sealed class RecordingEventPublisher : IEventPublisher
{
    private readonly List<object> _published = [];

    /// <summary>The published events, in order.</summary>
    public IReadOnlyList<object> Published => _published;

    /// <summary>When set, every publish fails with it.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Runs at each publish, before the event is kept, e.g. to see whether the save came first.</summary>
    public Action? OnPublish { get; set; }

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        OnPublish?.Invoke();
        _published.Add(integrationEvent);
        return Task.CompletedTask;
    }
}
