using BookingService.Application;
using BookingService.Clients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BookingService.IntegrationTests;

/// <summary>The service registers the client it asks Membership with (#256).</summary>
public sealed class MembershipClientRegistrationTests
{
    [Test]
    public void Start_WithMembershipsAddress_RegistersTheMembershipClient()
    {
        // Arrange
        using var app = new WebApplicationFactory<Program>();
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(builder =>
            builder.UseSetting(MembershipClient.BaseUrlConfigurationKey, "https://membership.test"));

        // Act
        IMembershipClient client = factory.Services.GetRequiredService<IMembershipClient>();

        // Assert
        Assert.That(client, Is.InstanceOf<MembershipClient>());
    }
}
