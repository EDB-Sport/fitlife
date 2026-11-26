using FitLife.Contracts.Testing;

namespace IdentityService.IntegrationTests;

/// <summary>The IdentityService rows in docs/domain/ACCESS.md match the service's real endpoints (#489).</summary>
public sealed class AccessTableTests
{
    [Test]
    public async Task AccessRows_MatchTheEndpoints()
    {
        // Arrange
        using var factory = new TestKeyFactory();

        // Act
        IReadOnlyList<string> differences = await AccessTable.CompareAsync("IdentityService", factory.Services).ConfigureAwait(false);

        // Assert
        Assert.That(differences, Is.Empty);
    }
}
