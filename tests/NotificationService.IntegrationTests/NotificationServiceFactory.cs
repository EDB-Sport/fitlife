using System.Globalization;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NotificationService.IntegrationTests;

/// <summary>NotificationService in memory, connected to the test run's RabbitMQ and MongoDB (<see cref="TestInfrastructure"/>).</summary>
/// <param name="logs">An extra logger provider, for tests that read the log lines; null for none.</param>
public sealed class NotificationServiceFactory(ILoggerProvider? logs = null) : WebApplicationFactory<Program>
{
    /// <summary>Waits until the service consumes ReservationMade. Start the host first, e.g. with <c>CreateClient()</c>.</summary>
    /// <returns>A task that completes when the queue is bound.</returns>
    public Task WaitUntilConsumingAsync()
    {
        return Services.GetServices<IHostedService>().OfType<RabbitMqEventConsumer<ReservationMade>>().Single()
            .Consuming.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>The <c>Consuming</c> task of every <see cref="RabbitMqEventConsumer{TEvent}"/> the host runs, one per event (#647).</summary>
    /// <returns>One task per consumer; it completes when that consumer's queue is bound.</returns>
    public IReadOnlyList<Task> Consumers()
    {
        return Services.GetServices<IHostedService>()
            .Where(service => service.GetType().IsGenericType && service.GetType().GetGenericTypeDefinition() == typeof(RabbitMqEventConsumer<>))
            .Select(consumer => consumer.GetType().GetProperty(nameof(RabbitMqEventConsumer<ReservationMade>.Consuming))?.GetValue(consumer) as Task
                ?? throw new InvalidOperationException($"{consumer.GetType()} has no Consuming task."))
            .ToList();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var rabbitMq = new Uri(TestInfrastructure.RabbitMq.GetConnectionString());
        string[] rabbitMqUser = rabbitMq.UserInfo.Split(':');
        var mongoDb = new Uri(TestInfrastructure.MongoDb.GetConnectionString());
        string[] mongoDbUser = mongoDb.UserInfo.Split(':');

        builder
            .UseSetting("RabbitMq:HostName", rabbitMq.Host)
            .UseSetting("RabbitMq:Port", rabbitMq.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("RabbitMq:UserName", rabbitMqUser[0])
            .UseSetting("RabbitMq:Password", rabbitMqUser[1])
            .UseSetting("MongoDb:Host", mongoDb.Host)
            .UseSetting("MongoDb:Port", mongoDb.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("MongoDb:Username", mongoDbUser[0])
            .UseSetting("MongoDb:Password", mongoDbUser[1])
            // The container's user is MongoDB's root user; in compose it is notification-svc in its own database.
            // The key is the one every service uses (#307)
            .UseSetting("MongoDb:AuthSource", "admin");
        if (logs is not null)
        {
            builder.ConfigureServices(services => services.AddSingleton(logs));
        }
    }
}
