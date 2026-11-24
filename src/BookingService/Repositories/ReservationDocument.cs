namespace BookingService.Repositories;

/// <summary>How a reservation is stored inside its class session.</summary>
internal sealed class ReservationDocument
{
    public Guid MemberId { get; init; }

    public bool IsFromWaitlist { get; init; }
}
