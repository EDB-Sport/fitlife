using FitLife.Contracts.Testing;

namespace NotificationService.IntegrationTests;

/// <summary>The NotificationService rows in docs/domain/ACCESS.md match the service's real endpoints (#489).</summary>
public sealed class AccessTableTests
{
    [Test]
    public async Task AccessRows_MatchTheEndpoints()
    {
        // Arrange
        using var factory = new NotificationServiceFactory();

        // Act
        IReadOnlyList<string> differences = await AccessTable.CompareAsync("NotificationService", factory.Services).ConfigureAwait(false);

        // Assert
        Assert.That(differences, Is.Empty);
    }
}
