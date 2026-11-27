using System.Text.Json.Serialization;

namespace BookingService.Api;

/// <summary>The body of <c>POST /class-sessions</c> (#238).</summary>
/// <param name="CenterId">The centre it takes place in.</param>
/// <param name="ClassTypeId">The kind of class it is a run of.</param>
/// <param name="InstructorName">Who teaches it.</param>
/// <param name="StartsAt">When it starts; after now.</param>
/// <param name="DurationMinutes">How long it lasts, in minutes; 1 or more.</param>
/// <param name="Capacity">How many members can have a reservation; 1 or more.</param>
public sealed record ScheduleClassSessionRequest(
    [property: JsonRequired] Guid CenterId,
    [property: JsonRequired] Guid ClassTypeId,
    string InstructorName,
    [property: JsonRequired] DateTimeOffset StartsAt,
    [property: JsonRequired] int DurationMinutes,
    [property: JsonRequired] int Capacity);
