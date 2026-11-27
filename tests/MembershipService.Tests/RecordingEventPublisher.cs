using FitLife.Messaging;

namespace MembershipService.Tests;

/// <summary>Keeps what a handler published, and can fail every publish as an unreachable RabbitMQ would (#251).</summary>
public sealed class RecordingEventPublisher : IEventPublisher
{
    private readonly List<object> _published = [];

    /// <summary>The published events, in order.</summary>
    public IReadOnlyList<object> Published => _published;

    /// <summary>When set, every publish throws it and nothing is kept.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Runs just before an event is kept, e.g. to see whether the save came first.</summary>
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
