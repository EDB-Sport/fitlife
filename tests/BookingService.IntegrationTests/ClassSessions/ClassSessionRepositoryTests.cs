using BookingService.Application;
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

    [Test]
    public async Task RoundTrip_AfterAChangedStart_KeepsTheNewStartAndWhoCancelsFree()
    {
        // Arrange
        var clock = new FakeTimeProvider(Now);
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", clock);
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(2), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        var memberId = Guid.NewGuid();
        classSession.Reserve(memberId, clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using (IServiceScope changing = factory.Services.CreateScope())
        {
            IClassSessionRepository repository = changing.ServiceProvider.GetRequiredService<IClassSessionRepository>();
            ClassSession stored = await repository.GetByIdAsync(classSession.Id, CancellationToken.None).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The class session wasn't stored.");
            stored.ChangeStart(Now.AddHours(1), clock);
            await repository.SaveAsync(stored, CancellationToken.None).ConfigureAwait(false);
        }

        using IServiceScope reading = factory.Services.CreateScope();
        IClassSessionRepository readingRepository = reading.ServiceProvider.GetRequiredService<IClassSessionRepository>();

        // Act
        ClassSession? changed = await readingRepository.GetByIdAsync(classSession.Id, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(changed?.StartTime, Is.EqualTo(Now.AddHours(1)));
            Assert.That(changed?.Reservations.Single().IsFromBeforeAChangedStart, Is.True);
        });
    }

    [Test]
    public async Task RoundTrip_ACancelledClassSession_ComesBackCancelled()
    {
        // Arrange
        var clock = new FakeTimeProvider(Now);
        using var factory = new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", clock);
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(2), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        classSession.Reserve(Guid.NewGuid(), clock);
        await factory.AddAsync(classSession).ConfigureAwait(false);
        using (IServiceScope cancelling = factory.Services.CreateScope())
        {
            IClassSessionRepository repository = cancelling.ServiceProvider.GetRequiredService<IClassSessionRepository>();
            ClassSession stored = await repository.GetByIdAsync(classSession.Id, CancellationToken.None).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The class session wasn't stored.");
            stored.Cancel();
            await repository.SaveAsync(stored, CancellationToken.None).ConfigureAwait(false);
        }

        using IServiceScope reading = factory.Services.CreateScope();
        IClassSessionRepository readingRepository = reading.ServiceProvider.GetRequiredService<IClassSessionRepository>();

        // Act
        ClassSession? cancelled = await readingRepository.GetByIdAsync(classSession.Id, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That((cancelled?.IsCancelled, cancelled?.Reservations.Count), Is.EqualTo(((bool?)true, (int?)0)));
    }
}
