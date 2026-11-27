namespace MemberPortal.Web.Clients;

/// <summary>Booking, reached through the gateway.</summary>
public interface IBookingClient
{
    /// <summary>The upcoming class sessions of every centre, by start, with <c>GET api/booking/class-sessions</c>. No log-in needed.</summary>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The class sessions, in Booking's order.</returns>
    /// <exception cref="BookingUnavailableException">Booking answered an error, a body that isn't a list, or nothing.</exception>
    Task<IReadOnlyList<ClassSessionResponse>> GetClassSessionsAsync(CancellationToken cancellationToken);
}
