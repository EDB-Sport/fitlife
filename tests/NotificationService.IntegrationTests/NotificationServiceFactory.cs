using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NotificationService.IntegrationTests;

/// <summary>NotificationService in memory, connected to the test run's RabbitMQ and MongoDB (<see cref="TestInfrastructure"/>).</summary>
/// <param name="logs">An extra logger provider, for tests that read the log lines; null for none.</param>
public sealed class NotificationServiceFactory(ILoggerProvider? logs = null) : WebApplicationFactory<Program>
{
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
            // The container's user is MongoDB's root user; in compose it is notification-svc in its own database
            .UseSetting("MongoDb:AuthDatabase", "admin");
        if (logs is not null)
        {
            builder.ConfigureServices(services => services.AddSingleton(logs));
        }
    }
}
