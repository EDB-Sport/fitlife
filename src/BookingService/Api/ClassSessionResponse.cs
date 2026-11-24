namespace BookingService.Api;

/// <summary>One class session in the class schedule.</summary>
/// <param name="Id">The class session's id.</param>
/// <param name="CenterId">The centre it takes place in.</param>
/// <param name="ClassTypeId">The kind of class it is a run of.</param>
/// <param name="StartTime">When it starts.</param>
/// <param name="Capacity">How many members can have a reservation.</param>
/// <param name="FreeSeats">How many seats are not reserved yet.</param>
public sealed record ClassSessionResponse(Guid Id, Guid CenterId, Guid ClassTypeId, DateTimeOffset StartTime, int Capacity, int FreeSeats);
