using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MembershipService.IntegrationTests;

/// <summary>
/// Starts MembershipService with the test signing key, and with a MongoDB when the test gives one.
/// Without one, MongoDB points at a port nobody listens on: the client connects only when a repository is used.
/// </summary>
public sealed class MembershipFactory(MongoDbFixture? mongo = null) : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        if (mongo is not null)
        {
            builder.UseSetting("MongoDb:Host", mongo.Host);
            builder.UseSetting("MongoDb:Port", mongo.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("MongoDb:Password", MongoDbFixture.ServicePassword);
        }
    }
}
