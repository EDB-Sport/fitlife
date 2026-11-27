extern alias booking;
extern alias notification;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using booking::BookingService.Application;
using booking::BookingService.Domain;
using Microsoft.Extensions.DependencyInjection;
using notification::NotificationService.Models;
using notification::NotificationService.Repositories;

namespace FitLife.Flows.IntegrationTests;

/// <summary>
/// Booking → Notification (#192): a reservation in Booking makes Notification store the member's notification, over the real
/// RabbitMQ. The event is done when this flow passes (<c>contracts/events/README.md</c>).
/// </summary>
public sealed class ReservationMadeFlowTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Test]
    public async Task PostReservation_InBooking_MakesNotificationStoreTheMembersNotification()
    {
        // Arrange
        using var notifications = new NotificationHost();
        await notifications.StartConsumingAsync(Timeout).ConfigureAwait(false);
        using var bookings = new BookingHost();
        // Whole minutes: the notification text shows the start to the minute
        var start = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(2).AddHours(17), TimeSpan.Zero);
        var classSession = new ClassSession(
            Guid.NewGuid(), new Capacity(5), start, new ClassSessionDetails(Guid.NewGuid(), Guid.NewGuid(), new Instructor("Mette"), TimeSpan.FromMinutes(60)));
        using (IServiceScope scope = bookings.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IClassSessionRepository>().AddAsync(classSession, CancellationToken.None).ConfigureAwait(false);
        }

        using HttpClient member = bookings.CreateClient();
        member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", FlowTokens.Medlem(Guid.NewGuid()));
        string correlationId = $"flow-{Guid.NewGuid():N}";
        member.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

        // Act
        using HttpResponseMessage response = await member.PostAsync(new Uri($"/class-sessions/{classSession.Id}/reservations", UriKind.Relative), content: null).ConfigureAwait(false);

        // Assert: Notification logs the line once the notification is stored, with the request's correlation ID; the
        // notification carries the stored reservation's id and the class session's start (the wire mapping)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        string line = await notifications.Logs.WaitForAsync($"Notification to member {bookings.MemberId}:", Timeout).ConfigureAwait(false);
        Guid reservationId = await StoredReservationIdAsync(bookings, classSession.Id).ConfigureAwait(false);
        Notification received = await NotificationForAsync(notifications, bookings.MemberId).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(line, Does.Contain(correlationId));
            Assert.That(received.Id, Is.EqualTo($"reservation-made:{reservationId}"));
            Assert.That(received.Text, Does.Contain(start.ToString("dd.MM.yyyy 'kl.' HH:mm", CultureInfo.InvariantCulture)));
        });
    }

    private static async Task<Guid> StoredReservationIdAsync(BookingHost bookings, Guid classSessionId)
    {
        using IServiceScope scope = bookings.Services.CreateScope();
        ClassSession? stored = await scope.ServiceProvider.GetRequiredService<IClassSessionRepository>().GetByIdAsync(classSessionId, CancellationToken.None).ConfigureAwait(false);
        return stored?.Reservations.Single().Id ?? Guid.Empty;
    }

    private static async Task<Notification> NotificationForAsync(NotificationHost notifications, Guid memberId)
    {
        using IServiceScope scope = notifications.Services.CreateScope();
        IReadOnlyList<Notification> stored = await scope.ServiceProvider.GetRequiredService<INotificationRepository>()
            .ListForMemberAsync(memberId.ToString(), CancellationToken.None).ConfigureAwait(false);
        return stored.Single();
    }
}
