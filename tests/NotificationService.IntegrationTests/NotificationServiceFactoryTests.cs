namespace NotificationService.IntegrationTests;

/// <summary>
/// <see cref="NotificationServiceFactory.WaitUntilConsumingAsync"/> waits for every event consumer (#647): /health has one check
/// per consumer, so a test that asks it after waiting for only one of them fails now and then with 503.
/// </summary>
public sealed class NotificationServiceFactoryTests
{
    [Test]
    public async Task WaitUntilConsumingAsync_ReturnsOnceEveryConsumerConsumes()
    {
        // Arrange
        using var factory = new NotificationServiceFactory();
        using HttpClient client = factory.CreateClient();

        // Act
        await factory.WaitUntilConsumingAsync().ConfigureAwait(false);

        // Assert
        IReadOnlyList<Task> consumers = factory.Consumers();
        Assert.That(consumers, Has.Count.GreaterThan(1));
        Assert.That(consumers.Select(consuming => consuming.IsCompletedSuccessfully), Is.All.True);
    }
}
