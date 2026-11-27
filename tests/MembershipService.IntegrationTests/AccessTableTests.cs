using FitLife.Contracts.Testing;

namespace MembershipService.IntegrationTests;

/// <summary>The MembershipService rows in docs/domain/ACCESS.md match the service's real endpoints (#489).</summary>
public sealed class AccessTableTests
{
    [Test]
    public async Task AccessRows_MatchTheEndpoints()
    {
        // Arrange
        using var factory = new MembershipFactory();

        // Act
        IReadOnlyList<string> differences = await AccessTable.CompareAsync("MembershipService", factory.Services).ConfigureAwait(false);

        // Assert
        Assert.That(differences, Is.Empty);
    }
}
