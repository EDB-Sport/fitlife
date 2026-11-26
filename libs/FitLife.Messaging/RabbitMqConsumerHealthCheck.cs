using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FitLife.Messaging;

/// <summary>
/// Unhealthy until the consumer has bound its queue, so <c>docker compose up --wait</c> and the Azure probes only see a
/// consuming service as up when events really reach it (#258).
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
/// <param name="consumer">The consumer it reports on.</param>
public sealed class RabbitMqConsumerHealthCheck<TEvent>(RabbitMqEventConsumer<TEvent> consumer) : IHealthCheck
    where TEvent : IIntegrationEvent
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (consumer.Consuming.IsCompletedSuccessfully)
        {
            return Task.FromResult(HealthCheckResult.Healthy($"Consuming {consumer.Queue}"));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy($"Not consuming {consumer.Queue} yet"));
    }
}
