using BookingService.Application;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.Api;

/// <summary>The class schedule over HTTP (#254). <see cref="GetClassSessionsQueryHandler"/> does the work.</summary>
[ApiController]
[Route("class-sessions")]
public sealed class ClassScheduleController : ControllerBase
{
    private readonly GetClassSessionsQueryHandler _classSchedule;

    /// <summary>Creates the controller.</summary>
    /// <param name="classSchedule">Answers the class schedule.</param>
    public ClassScheduleController(GetClassSessionsQueryHandler classSchedule)
    {
        _classSchedule = classSchedule;
    }

    /// <summary>
    /// Lists the class sessions that start after now, by start, of a centre or of every centre, with their free seats (a cancelled
    /// one has none) and the names of their centre and class type. No login needed.
    /// </summary>
    /// <param name="centerId">The centre; leave it out for every centre.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClassSessionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassSessionResponse>>> GetClassSessions([FromQuery] Guid? centerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ClassSessionSummary> schedule = await _classSchedule.Handle(new GetClassSessionsQuery(centerId), cancellationToken).ConfigureAwait(false);
        return Ok(schedule.Select(summary => new ClassSessionResponse(
            summary.Id,
            summary.CenterId,
            summary.CenterName,
            summary.ClassTypeId,
            summary.ClassTypeName,
            summary.InstructorName,
            summary.StartsAt,
            summary.DurationMinutes,
            summary.Capacity,
            summary.FreeSeats,
            summary.IsCancelled)).ToList());
    }
}
