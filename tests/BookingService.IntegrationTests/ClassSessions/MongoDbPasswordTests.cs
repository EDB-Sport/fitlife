using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>
/// Compose and Azure give the connection string without its password (it names the user only), and the password in
/// MongoDb:Password, from Vault (#17). The service must connect with that pair (#171).
/// </summary>
public sealed class MongoDbPasswordTests
{
    [Test]
    public async Task GetClassSessions_WithThePasswordInItsOwnSetting_Returns200()
    {
        // Arrange
        var url = new Uri(MongoDbContainerSetUp.ConnectionString);
        string[] userInfo = url.UserInfo.Split(':');
        string withoutPassword = $"{url.Scheme}://{userInfo[0]}@{url.Authority}/?authSource=admin";
        using var factory = new BookingApiFactory(withoutPassword, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider());
        using WebApplicationFactory<Program> withPassword = factory.WithWebHostBuilder(host => host.UseSetting("MongoDb:Password", Uri.UnescapeDataString(userInfo[1])));
        using HttpClient client = withPassword.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri("/class-sessions", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
