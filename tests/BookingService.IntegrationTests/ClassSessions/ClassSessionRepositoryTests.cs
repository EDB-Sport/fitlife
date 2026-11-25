using BookingService.Domain;
using BookingService.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>Two requests that change the same class session at once can't both save, so the capacity holds (#171).</summary>
public sealed class ClassSessionRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Save_AfterAnotherRequestSavedTheSameClassSession_ReportsAConflict()
    {
        // Arrange
        var clock = new FakeTimeProvider(Now);
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", clock);
        var classSession = new ClassSession(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new Capacity(1), Now.AddDays(1));
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using IServiceScope first = factory.Services.CreateScope();
        using IServiceScope second = factory.Services.CreateScope();
        IClassSessionRepository firstRepository = first.ServiceProvider.GetRequiredService<IClassSessionRepository>();
        IClassSessionRepository secondRepository = second.ServiceProvider.GetRequiredService<IClassSessionRepository>();
        ClassSession firstCopy = await firstRepository.GetAsync(classSession.Id, CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The class session wasn't stored.");
        ClassSession secondCopy = await secondRepository.GetAsync(classSession.Id, CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The class session wasn't stored.");
        firstCopy.Reserve(Guid.NewGuid(), clock);
        secondCopy.Reserve(Guid.NewGuid(), clock);
        await firstRepository.SaveAsync(firstCopy, CancellationToken.None).ConfigureAwait(false);

        // Act
        bool saved = await secondRepository.SaveAsync(secondCopy, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(saved, Is.False);
    }
}
