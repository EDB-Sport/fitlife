namespace McpServer.Booking;

/// <summary>Asks Booking for the class schedule through the gateway (CONTEXT-MAP section 4). The schedule is Booking's: this only passes it on.</summary>
public interface IBookingClient
{
    /// <summary>Asks for the upcoming class sessions of a centre, or of every centre, in the order Booking answers them.</summary>
    /// <param name="centerId">The centre; <see langword="null"/> for every centre.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The class sessions Booking answers, none changed.</returns>
    /// <exception cref="BookingUnavailableException">Booking answered an error, an answer that can't be read, or didn't answer at all.</exception>
    Task<IReadOnlyList<ClassSession>> GetClassSessionsAsync(Guid? centerId, CancellationToken cancellationToken);
}
