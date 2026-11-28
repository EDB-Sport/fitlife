namespace BookingService.Repositories;

/// <summary>How a reservation is stored inside its class session.</summary>
internal sealed class ReservationDocument
{
    /// <summary>The reservation's id; <see cref="Guid.Empty"/> in a document stored before #192.</summary>
    public Guid Id { get; init; }

    public Guid MemberId { get; init; }

    public bool IsFromWaitlist { get; init; }

    /// <summary>Reserved before the class session's start changed, so it cancels free (#240, #241). False in older documents.</summary>
    public bool IsFromBeforeAChangedStart { get; init; }
}
