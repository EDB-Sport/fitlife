using BookingService.Domain;

namespace BookingService.Application;

/// <summary>One class session as the class schedule shows it (#254).</summary>
/// <param name="Id">The class session's id.</param>
/// <param name="CenterId">The centre it takes place in.</param>
/// <param name="CenterName">The centre's name; empty when the centre isn't in the reference data.</param>
/// <param name="ClassTypeId">The kind of class it is a run of.</param>
/// <param name="ClassTypeName">The class type's name; empty when the class type isn't in the reference data.</param>
/// <param name="InstructorName">Who teaches it.</param>
/// <param name="StartTime">When it starts.</param>
/// <param name="DurationMinutes">How long it lasts.</param>
/// <param name="Capacity">How many members can have a reservation.</param>
/// <param name="FreeSeats">How many seats are not reserved yet; 0 when the class session is cancelled.</param>
/// <param name="IsCancelled">The class session will not take place (Hold aflyst).</param>
public sealed record ClassSessionSummary(
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
    bool IsCancelled)
{
    /// <summary>The summary of a class session, with the names of its centre and class type.</summary>
    /// <param name="classSession">The class session.</param>
    /// <param name="centerName">The centre's name.</param>
    /// <param name="classTypeName">The class type's name.</param>
    public static ClassSessionSummary Of(ClassSession classSession, string centerName, string classTypeName)
    {
        ArgumentNullException.ThrowIfNull(classSession);
        return new ClassSessionSummary(
            classSession.Id,
            classSession.CenterId,
            centerName,
            classSession.ClassTypeId,
            classTypeName,
            classSession.Details.Instructor.Name,
            classSession.StartTime,
            (int)classSession.Details.Duration.TotalMinutes,
            classSession.Capacity.Value,
            classSession.FreeSeats,
            classSession.IsCancelled);
    }
}
