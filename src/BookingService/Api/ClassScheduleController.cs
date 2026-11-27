using System.Diagnostics;
using BookingService.Application;
using FitLife.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.Api;

/// <summary>
/// The class schedule over HTTP: reading it (#254, <see cref="GetClassSessionsQueryHandler"/>) and scheduling a class session on it
/// (#238, <see cref="ScheduleClassSessionCommandHandler"/>).
/// </summary>
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
        return Ok(schedule.Select(ToResponse).ToList());
    }

    /// <summary>Schedules a class session (#238). It then shows in the class schedule, and members can reserve a seat on it.</summary>
    /// <param name="request">Where, what, with whom, when, how long and how many seats.</param>
    /// <param name="scheduleClassSession">Checks the request and saves the class session.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <response code="400">The start is not after now, the capacity or the duration is under 1, or the instructor's name is missing.</response>
    /// <response code="403">The token has neither the role Træner nor Admin.</response>
    /// <response code="404">No such center or class type.</response>
    [HttpPost]
    [Authorize(Roles = FitLifeRoles.Traener + "," + FitLifeRoles.Admin)]
    [Consumes("application/json")]
    [ProducesResponseType<ClassSessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ClassSessionResponse>> PostClassSession(
        ScheduleClassSessionRequest request,
        [FromServices] ScheduleClassSessionCommandHandler scheduleClassSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(scheduleClassSession);
        var command = new ScheduleClassSessionCommand(
            request.CenterId,
            request.ClassTypeId,
            request.InstructorName,
            request.StartsAt,
            request.DurationMinutes,
            request.Capacity);
        ScheduleClassSessionResult result = await scheduleClassSession.Handle(command, cancellationToken).ConfigureAwait(false);
        switch (result.Outcome)
        {
            case ScheduleClassSessionOutcome.Scheduled when result.ClassSession is { } classSession:
                return StatusCode(StatusCodes.Status201Created, ToResponse(classSession));
            case ScheduleClassSessionOutcome.StartNotInTheFuture:
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "The start time must be after now.");
            case ScheduleClassSessionOutcome.CapacityUnderOne:
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "The capacity must be at least 1.");
            case ScheduleClassSessionOutcome.DurationUnderOneMinute:
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "The duration must be at least 1 minute.");
            case ScheduleClassSessionOutcome.InstructorNameMissing:
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "The instructor's name is missing.");
            case ScheduleClassSessionOutcome.CenterNotFound:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "No such center.");
            case ScheduleClassSessionOutcome.ClassTypeNotFound:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "No such class type.");
            default:
                throw new UnreachableException($"ScheduleClassSessionOutcome.{result.Outcome} has no status code.");
        }
    }

    private static ClassSessionResponse ToResponse(ClassSessionSummary summary)
    {
        return new ClassSessionResponse(
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
            summary.IsCancelled);
    }
}
