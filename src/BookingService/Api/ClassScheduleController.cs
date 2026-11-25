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

    /// <summary>Lists the upcoming class sessions of a centre, or of every centre, by start, with names and free seats. No login needed.</summary>
    /// <remarks>Only class sessions that start after now. A cancelled one has no free seats. An unknown centre or class type has an empty name.</remarks>
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
            summary.StartTime,
            summary.DurationMinutes,
            summary.Capacity,
            summary.FreeSeats,
            summary.IsCancelled)).ToList());
    }
}
