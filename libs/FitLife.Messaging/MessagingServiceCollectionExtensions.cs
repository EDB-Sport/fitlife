using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FitLife.Messaging;

/// <summary>Registers RabbitMQ messaging in a service's <c>Program.cs</c>.</summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>The connection, from the <c>RabbitMq</c> configuration section. Needed by the publisher and the consumers.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The service's configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.Section));
        services.AddSingleton<RabbitMqConnection>();
        return services;
    }

    /// <summary>An <see cref="IEventPublisher"/> for the publishing service.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddEventPublisher(this IServiceCollection services)
    {
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        return services;
    }

    /// <summary>
    /// A consumer for one event type. Register the <see cref="IEventSubscriber{TEvent}"/> for it as well.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="queuePrefix">The consuming service, e.g. <c>notification</c>.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddEventConsumer<TEvent>(this IServiceCollection services, string queuePrefix)
        where TEvent : IIntegrationEvent
    {
        services.AddHostedService(provider => new RabbitMqEventConsumer<TEvent>(
            provider.GetRequiredService<RabbitMqConnection>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<RabbitMqEventConsumer<TEvent>>>(),
            queuePrefix));
        return services;
    }
}
