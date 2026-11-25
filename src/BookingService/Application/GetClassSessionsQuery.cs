namespace BookingService.Application;

/// <summary>Asks for the class schedule: the class sessions that start after now (#254).</summary>
/// <param name="CenterId">The centre, or null for every centre.</param>
public sealed record GetClassSessionsQuery(Guid? CenterId);
