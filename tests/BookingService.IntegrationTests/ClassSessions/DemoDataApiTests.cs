using System.Net.Http.Json;
using BookingService.Api;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>The demo data in MongoDB: the class schedule names the seeded centres and class types (#255).</summary>
public sealed class DemoDataApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task GetClassSessions_AfterTheSeed_ListsCentreAndClassTypeNames()
    {
        // Arrange
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider(Now), seed: true);
        using HttpClient client = factory.CreateClient();

        // Act
        ClassSessionResponse[]? schedule = await client.GetFromJsonAsync<ClassSessionResponse[]>(new Uri("/class-sessions", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(schedule, Is.Not.Empty);
            Assert.That(schedule?.Select(classSession => classSession.CenterName), Is.All.StartsWith("FitLife Center "));
            Assert.That(schedule?.Select(classSession => classSession.ClassTypeName), Is.All.Not.Empty);
        });
    }
}
