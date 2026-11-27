using FitLife.Messaging;
using MembershipService.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MembershipService.IntegrationTests;

/// <summary>
/// Starts MembershipService with the test signing key, and with a MongoDB when the test gives one.
/// Without one, MongoDB points at a port nobody listens on: the client connects only when a repository is used.
/// The payment provider is a stub that accepts every charge: the provider is outside FitLife, and its mock is tested in Postman (#174).
/// </summary>
public sealed class MembershipFactory(MongoDbFixture? mongo = null) : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", TestTokens.SigningKey);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IPaymentProvider>(new AcceptingPaymentProvider());

            // No RabbitMQ in these tests: the handler tests check what is published (#251)
            services.AddSingleton<IEventPublisher>(new DiscardingEventPublisher());
        });
        if (mongo is not null)
        {
            builder.UseSetting("MongoDb:Host", mongo.Host);
            builder.UseSetting("MongoDb:Port", mongo.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("MongoDb:Password", MongoDbFixture.ServicePassword);
        }
    }

    private sealed class DiscardingEventPublisher : IEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent
        {
            return Task.CompletedTask;
        }
    }

    private sealed class AcceptingPaymentProvider : IPaymentProvider
    {
        public Task<bool> ChargeMembershipFeeAsync(Guid memberId, decimal amountDkk, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }
}
