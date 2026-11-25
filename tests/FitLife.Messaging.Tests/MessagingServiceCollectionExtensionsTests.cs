using FitLife.Messaging.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FitLife.Messaging.Tests;

/// <summary>What a service gets from the registration methods in its Program.cs (#190).</summary>
public sealed class MessagingServiceCollectionExtensionsTests
{
    [Test]
    public void AddRabbitMq_ReadsTheRabbitMqSection()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["RabbitMq:HostName"] = "rabbitmq",
            ["RabbitMq:Port"] = "5673",
            ["RabbitMq:UserName"] = "fitlife",
            ["RabbitMq:Password"] = "secret",
        }).Build();

        // Act
        using ServiceProvider services = new ServiceCollection().AddRabbitMq(configuration).BuildServiceProvider();

        // Assert
        RabbitMqOptions options = services.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        Assert.That((options.HostName, options.Port, options.UserName, options.Password), Is.EqualTo(("rabbitmq", 5673, "fitlife", "secret")));
    }

    [Test]
    public void AddEventPublisher_RegistersTheRabbitMqPublisher()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection().AddRabbitMq(new ConfigurationBuilder().Build());

        // Act
        using ServiceProvider provider = services.AddEventPublisher().BuildServiceProvider();

        // Assert
        Assert.That(provider.GetRequiredService<IEventPublisher>(), Is.TypeOf<RabbitMqEventPublisher>());
    }

    [Test]
    public void AddEventConsumer_RegistersAHostedConsumerOnTheServicesQueue()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection()
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>))
            .AddRabbitMq(new ConfigurationBuilder().Build());

        // Act
        using ServiceProvider provider = services.AddEventConsumer<ReservationMade>("notification").BuildServiceProvider();

        // Assert
        RabbitMqEventConsumer<ReservationMade> consumer = provider.GetServices<IHostedService>().OfType<RabbitMqEventConsumer<ReservationMade>>().Single();
        Assert.That(consumer.Queue, Is.EqualTo("notification.reservation-made"));
    }
}
