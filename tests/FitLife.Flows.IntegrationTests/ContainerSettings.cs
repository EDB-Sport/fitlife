using System.Globalization;
using Microsoft.AspNetCore.Hosting;

namespace FitLife.Flows.IntegrationTests;

/// <summary>Points a host at the flow tests' RabbitMQ and MongoDB.</summary>
internal static class ContainerSettings
{
    /// <summary>Sets the <c>RabbitMq</c> and <c>MongoDb</c> settings, with its own database.</summary>
    /// <param name="builder">The host.</param>
    /// <param name="database">The service's database.</param>
    public static void UseContainers(this IWebHostBuilder builder, string database)
    {
        var rabbitMq = new Uri(FlowInfrastructure.RabbitMq.GetConnectionString());
        string[] rabbitMqUser = rabbitMq.UserInfo.Split(':');
        var mongoDb = new Uri(FlowInfrastructure.MongoDb.GetConnectionString());
        string[] mongoDbUser = mongoDb.UserInfo.Split(':');
        builder
            .UseSetting("RabbitMq:HostName", rabbitMq.Host)
            .UseSetting("RabbitMq:Port", rabbitMq.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("RabbitMq:UserName", rabbitMqUser[0])
            .UseSetting("RabbitMq:Password", rabbitMqUser[1])
            .UseSetting("MongoDb:Host", mongoDb.Host)
            .UseSetting("MongoDb:Port", mongoDb.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("MongoDb:Username", Uri.UnescapeDataString(mongoDbUser[0]))
            .UseSetting("MongoDb:Password", Uri.UnescapeDataString(mongoDbUser[1]))
            .UseSetting("MongoDb:AuthSource", "admin") // the container's root user; in compose each service has its own
            .UseSetting("MongoDb:Database", database);
    }
}
