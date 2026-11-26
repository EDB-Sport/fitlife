namespace BookingService.Application;

/// <summary>A Træner or an Admin schedules a class session (#238).</summary>
/// <param name="CenterId">The centre it takes place in.</param>
/// <param name="ClassTypeId">The kind of class it is a run of.</param>
/// <param name="InstructorName">Who teaches it.</param>
/// <param name="StartsAt">When it starts.</param>
/// <param name="DurationMinutes">How long it lasts.</param>
/// <param name="Capacity">How many members can have a reservation.</param>
public sealed record ScheduleClassSessionCommand(
    Guid CenterId,
    Guid ClassTypeId,
    string InstructorName,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    int Capacity);
