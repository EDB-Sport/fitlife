using FitLife.Contracts.Testing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BookingService.IntegrationTests;

/// <summary>The BookingService rows in docs/domain/ACCESS.md match the service's real endpoints (#489).</summary>
public sealed class AccessTableTests
{
    [Test]
    public async Task AccessRows_MatchTheEndpoints()
    {
        // Arrange
        using var factory = new WebApplicationFactory<Program>();

        // Act
        IReadOnlyList<string> differences = await AccessTable.CompareAsync("BookingService", factory.Services).ConfigureAwait(false);

        // Assert
        Assert.That(differences, Is.Empty);
    }
}
