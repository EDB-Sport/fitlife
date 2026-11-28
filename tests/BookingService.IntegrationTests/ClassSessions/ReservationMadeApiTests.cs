using System.Net;
using BookingService.Application;
using BookingService.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ReservationMadeEvent = FitLife.Messaging.Events.ReservationMade;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>
/// Booking publishes <c>ReservationMade</c> after the seat is stored, directly or from the waitlist, and the id it sends is the
/// stored reservation's (#192).
/// </summary>
public sealed class ReservationMadeApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task PostReservation_PublishesReservationMadeWithTheStoredReservationsId()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory, capacity: 1).ConfigureAwait(false);
        var memberId = Guid.NewGuid();
        factory.Membership.Answer = MemberLookup.Found(memberId, isActive: true);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PostAsync(new Uri($"/class-sessions/{classSession.Id}/reservations", UriKind.Relative), content: null).ConfigureAwait(false);

        // Assert
        Reservation stored = (await ReadAsync(factory, classSession.Id).ConfigureAwait(false)).Reservations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(
                factory.Events.Published,
                Is.EqualTo(new object[] { new ReservationMadeEvent(stored.Id.ToString(), classSession.Id.ToString(), memberId.ToString(), classSession.StartTime) }));
        });
    }

    [Test]
    public async Task DeleteReservation_WithAWaitlist_PublishesReservationMadeForTheFirstWaitlistEntry()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        var memberId = Guid.NewGuid();
        var waitingMemberId = Guid.NewGuid();
        ClassSession classSession = await StoredAsync(factory, capacity: 1, memberId, waitingMemberId).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(new Uri($"/class-sessions/{classSession.Id}/reservations/{memberId}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Reservation fromWaitlist = (await ReadAsync(factory, classSession.Id).ConfigureAwait(false)).Reservations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(
                factory.Events.Published,
                Is.EqualTo(new object[] { new ReservationMadeEvent(fromWaitlist.Id.ToString(), classSession.Id.ToString(), waitingMemberId.ToString(), classSession.StartTime) }));
        });
    }

    [Test]
    public async Task DeleteReservation_WithAnEmptyWaitlist_PublishesNoReservationMade()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        var memberId = Guid.NewGuid();
        ClassSession classSession = await StoredAsync(factory, capacity: 1, memberId).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(new Uri($"/class-sessions/{classSession.Id}/reservations/{memberId}", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(factory.Events.Published.OfType<ReservationMadeEvent>(), Is.Empty);
    }

    [Test]
    public async Task RoundTrip_KeepsTheReservationId()
    {
        // Arrange
        using BookingApiFactory factory = Factory();
        ClassSession classSession = await StoredAsync(factory, capacity: 1, Guid.NewGuid()).ConfigureAwait(false);

        // Act
        ClassSession stored = await ReadAsync(factory, classSession.Id).ConfigureAwait(false);

        // Assert
        Assert.That(stored.Reservations.Single().Id, Is.EqualTo(classSession.Reservations.Single().Id));
    }

    private static async Task<ClassSession> StoredAsync(BookingApiFactory factory, int capacity, params Guid[] memberIds)
    {
        var classSession = new ClassSession(
            Guid.NewGuid(), new Capacity(capacity), Now.AddDays(1), new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        foreach (Guid memberId in memberIds)
        {
            if (classSession.Reserve(memberId, factory.Clock) != ReservationResult.Made)
            {
                classSession.JoinWaitlist(memberId, factory.Clock);
            }
        }

        await factory.AddAsync(classSession).ConfigureAwait(false);
        return classSession;
    }

    private static async Task<ClassSession> ReadAsync(BookingApiFactory factory, Guid classSessionId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IClassSessionRepository>().GetByIdAsync(classSessionId, CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The class session wasn't stored.");
    }

    private static BookingApiFactory Factory()
    {
        return new BookingApiFactory(MongoDbContainerSetUp.ConnectionString, $"booking-{Guid.NewGuid():N}", new FakeTimeProvider(Now));
    }
}
