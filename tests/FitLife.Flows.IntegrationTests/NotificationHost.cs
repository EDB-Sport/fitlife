extern alias notification;

using FitLife.Messaging;
using FitLife.Messaging.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitLife.Flows.IntegrationTests;

/// <summary>NotificationService on the flow tests' containers, nothing faked.</summary>
internal sealed class NotificationHost : WebApplicationFactory<notification::Program>
{
    /// <summary>Starts the host and waits until its ReservationMade queue is bound, so no published event is missed.</summary>
    /// <param name="timeout">How long to wait.</param>
    /// <returns>A task that completes when it consumes.</returns>
    public Task StartConsumingAsync(TimeSpan timeout)
    {
        CreateClient().Dispose(); // starts the host; the consumer binds its queue in the background
        return Services.GetServices<IHostedService>().OfType<RabbitMqEventConsumer<ReservationMade>>().Single().Consuming.WaitAsync(timeout);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContainers($"notification-{Guid.NewGuid():N}");
    }
}
