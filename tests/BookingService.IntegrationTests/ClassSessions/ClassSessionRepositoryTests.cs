using BookingService.Domain;
using BookingService.Application;
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
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(1), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using IServiceScope first = factory.Services.CreateScope();
        using IServiceScope second = factory.Services.CreateScope();
        IClassSessionRepository firstRepository = first.ServiceProvider.GetRequiredService<IClassSessionRepository>();
        IClassSessionRepository secondRepository = second.ServiceProvider.GetRequiredService<IClassSessionRepository>();
        ClassSession firstCopy = await firstRepository.GetByIdAsync(classSession.Id, CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The class session wasn't stored.");
        ClassSession secondCopy = await secondRepository.GetByIdAsync(classSession.Id, CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The class session wasn't stored.");
        firstCopy.Reserve(Guid.NewGuid(), clock);
        secondCopy.Reserve(Guid.NewGuid(), clock);
        await firstRepository.SaveAsync(firstCopy, CancellationToken.None).ConfigureAwait(false);

        // Act
        bool saved = await secondRepository.SaveAsync(secondCopy, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(saved, Is.False);
    }

    [Test]
    public async Task Get_AfterAdd_ReturnsTheDetails()
    {
        // Arrange
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider(Now));
        var details = new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(45));
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(1), Now.AddDays(1), details);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using IServiceScope scope = factory.Services.CreateScope();
        IClassSessionRepository repository = scope.ServiceProvider.GetRequiredService<IClassSessionRepository>();

        // Act
        ClassSession? stored = await repository.GetByIdAsync(classSession.Id, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(stored?.Details, Is.EqualTo(details));
    }
}
