namespace McpServer.Booking;

/// <summary>
/// One class session (Hold) in Booking's class schedule, with the fields of <c>ClassSessionResponse</c> in
/// <c>contracts/openapi/bookingservice.json</c>. The server's own record: it references no code from Booking (FITLIFE001).
/// </summary>
/// <param name="Id">The class session's id.</param>
/// <param name="CenterId">The centre it takes place in.</param>
/// <param name="CenterName">The centre's name; empty when Booking doesn't know the centre.</param>
/// <param name="ClassTypeId">The kind of class it is a run of.</param>
/// <param name="ClassTypeName">The class type's name; empty when Booking doesn't know the class type.</param>
/// <param name="InstructorName">Who teaches it.</param>
/// <param name="StartTime">When it starts.</param>
/// <param name="DurationMinutes">How long it lasts, in minutes.</param>
/// <param name="Capacity">How many members can have a reservation.</param>
/// <param name="FreeSeats">How many seats are not reserved yet; 0 when the class session is cancelled.</param>
/// <param name="IsCancelled">The class session will not take place (Hold aflyst).</param>
public sealed record ClassSession(
    Guid Id,
    Guid CenterId,
    string CenterName,
    Guid ClassTypeId,
    string ClassTypeName,
    string InstructorName,
    DateTimeOffset StartTime,
    int DurationMinutes,
    int Capacity,
    int FreeSeats,
    bool IsCancelled);
