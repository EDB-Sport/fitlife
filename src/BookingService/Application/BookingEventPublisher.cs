using BookingService.Domain;
using FitLife.Messaging;
using ReservationMadeEvent = FitLife.Messaging.Events.ReservationMade;

namespace BookingService.Application;

/// <summary>
/// Publishes Booking's integration events after the change is saved (#192). A failed publish is logged and the change is
/// kept, so the member's answer is the same (#192's safe default; no outbox, so the event is lost).
/// </summary>
/// <param name="events">The RabbitMQ publisher.</param>
/// <param name="logger">Logs a publish that failed.</param>
public sealed partial class BookingEventPublisher(IEventPublisher events, ILogger<BookingEventPublisher> logger)
{
    /// <summary>Publishes <c>ReservationMade</c> for the member's reservation on the class session.</summary>
    /// <param name="classSession">The saved class session.</param>
    /// <param name="memberId">The member who got the seat.</param>
    /// <returns>A task that completes when the event is published or the failure is logged.</returns>
    public Task PublishReservationMadeAsync(ClassSession classSession, Guid memberId)
    {
        ArgumentNullException.ThrowIfNull(classSession);
        Reservation reservation = classSession.Reservations.Single(seat => seat.MemberId == memberId);
        return PublishAsync(new ReservationMadeEvent(reservation.Id.ToString(), classSession.Id.ToString(), memberId.ToString(), classSession.StartTime));
    }

    /// <summary>Publishes <c>ReservationMade</c> for every seat the class session's domain events say a waitlist entry got.</summary>
    /// <param name="classSession">The saved class session.</param>
    /// <returns>A task that completes when the events are published or the failures are logged.</returns>
    public async Task PublishReservationsMadeAsync(ClassSession classSession)
    {
        ArgumentNullException.ThrowIfNull(classSession);
        foreach (ReservationMade reservationMade in classSession.DomainEvents.OfType<ReservationMade>())
        {
            await PublishReservationMadeAsync(classSession, reservationMade.MemberId).ConfigureAwait(false);
        }
    }

    private async Task PublishAsync<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent
    {
        try
        {
            // Not the request's token: the change is saved, so a caller that goes away doesn't stop its event either
            await events.PublishAsync(integrationEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException) // none of RabbitMQ's errors may undo the saved change
        {
            LogPublishFailed(TEvent.RoutingKey, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The change is saved, but {RoutingKey} wasn't published")]
    private partial void LogPublishFailed(string routingKey, Exception exception);
}
