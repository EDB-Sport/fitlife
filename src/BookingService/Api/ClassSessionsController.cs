using System.Diagnostics;
using BookingService.Application;
using BookingService.Domain;
using FitLife.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.Api;

/// <summary>
/// Reservations and the waitlist over HTTP (#171); the class schedule is <see cref="ClassScheduleController"/>. The rules live in <see cref="ClassSession"/>; this class only
/// loads it, calls it, saves it and turns its answer into a status code.
/// </summary>
[ApiController]
[Route("class-sessions")]
public sealed class ClassSessionsController : ControllerBase
{
    private const string ClassSessionCancelledTitle = "The class session is cancelled.";

    private readonly IClassSessionRepository _repository;
    private readonly TimeProvider _clock;

    /// <summary>Creates the controller.</summary>
    /// <param name="repository">Where class sessions are stored.</param>
    /// <param name="clock">Tells the time now.</param>
    public ClassSessionsController(IClassSessionRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>Reserves a seat for the member behind the token (#260). Membership says who the member is and whether they may book.</summary>
    /// <param name="id">The class session.</param>
    /// <param name="reserveSeat">Asks Membership, then the class session.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <response code="403">The token has no role Medlem, or the account has no active membership (code MembershipNotActive).</response>
    /// <response code="409">The class session rejected the reservation; code is the reason, e.g. ClassSessionFull or TooFarAhead.</response>
    /// <response code="503">Membership didn't answer (code MembershipUnavailable).</response>
    [HttpPost("{id:guid}/reservations")]
    [Authorize(Roles = FitLifeRoles.Medlem)]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ReservationResponse>> PostReservation(Guid id, [FromServices] ReserveSeatCommandHandler reserveSeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reserveSeat);
        if (!Guid.TryParse(User.GetAccountId(), out Guid accountId))
        {
            return ReservationProblems.Create(this, StatusCodes.Status403Forbidden, "The token's account id is not an account.", nameof(ReserveSeatOutcome.MembershipNotActive));
        }

        string bearerToken = Request.Headers.Authorization.ToString()["Bearer ".Length..];
        ReserveSeatResult result = await reserveSeat.Handle(new ReserveSeatCommand(id, accountId, bearerToken), cancellationToken).ConfigureAwait(false);
        switch (result.Outcome)
        {
            case ReserveSeatOutcome.Answered when result.Reservation == ReservationResult.Made:
                return StatusCode(StatusCodes.Status201Created, new ReservationResponse(id, result.MemberId));
            case ReserveSeatOutcome.Answered:
                return ReservationProblems.Rejected(this, result.Reservation);
            case ReserveSeatOutcome.MembershipNotActive:
                return ReservationProblems.Create(this, StatusCodes.Status403Forbidden, "Only an active membership gives the right to book.", result.Outcome.ToString());
            case ReserveSeatOutcome.MembershipUnavailable:
                return ReservationProblems.Create(this, StatusCodes.Status503ServiceUnavailable, "Membership didn't answer. Try again.", result.Outcome.ToString());
            case ReserveSeatOutcome.ClassSessionNotFound:
                return ReservationProblems.Create(this, StatusCodes.Status404NotFound, "No such class session.", result.Outcome.ToString());
            case ReserveSeatOutcome.ConcurrentChange:
                return ReservationProblems.Create(this, StatusCodes.Status409Conflict, ConcurrentChangeExceptionHandler.Title, result.Outcome.ToString());
            default:
                throw new UnreachableException($"ReserveSeatOutcome.{result.Outcome} has no status code.");
        }
    }

    /// <summary>Cancels the member's reservation. When someone is on the waitlist, the first waitlist entry gets the seat.</summary>
    /// <param name="id">The class session.</param>
    /// <param name="memberId">The member who gives up the seat.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <response code="404">No such class session, or the member has no seat on it.</response>
    [HttpDelete("{id:guid}/reservations/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<Results<NoContent, ProblemHttpResult>> DeleteReservation(Guid id, Guid memberId, CancellationToken cancellationToken)
    {
        ClassSession? classSession = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (classSession is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "No such class session.", detail: $"There is no class session {id}.");
        }

        if (classSession.CancelReservation(memberId, _clock) == CancellationResult.NoReservation)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "The member has no seat on this class session.");
        }

        await _repository.SaveAsync(classSession, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    /// <summary>Puts the member at the end of the waitlist of a full class session.</summary>
    /// <param name="id">The class session.</param>
    /// <param name="request">The member who wants to wait for a seat.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <response code="409">A seat is free, the waitlist has closed, the class session is cancelled, or the member already has a place.</response>
    [HttpPost("{id:guid}/waitlist")]
    [Consumes("application/json")]
    [ProducesResponseType<WaitlistEntryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<WaitlistEntryResponse>> PostWaitlistEntry(Guid id, WaitlistEntryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClassSession? classSession = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (classSession is null)
        {
            return ClassSessionNotFound(id);
        }

        WaitlistResult result = classSession.JoinWaitlist(request.MemberId, _clock);
        switch (result)
        {
            case WaitlistResult.Joined:
                break;
            case WaitlistResult.ClassSessionNotFull:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "A seat is free. Reserve it instead.");
            case WaitlistResult.WaitlistClosed:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "The waitlist closed 2 hours before start.");
            case WaitlistResult.MemberAlreadyHasReservation:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "The member already has a seat on this class session.");
            case WaitlistResult.MemberAlreadyOnWaitlist:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "The member is already on the waitlist of this class session.");
            case WaitlistResult.ClassSessionCancelled:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: ClassSessionCancelledTitle);
            default:
                throw new UnreachableException($"WaitlistResult.{result} has no status code.");
        }

        await _repository.SaveAsync(classSession, cancellationToken).ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, new WaitlistEntryResponse(id, request.MemberId));
    }

    private ObjectResult ClassSessionNotFound(Guid id)
    {
        return Problem(statusCode: StatusCodes.Status404NotFound, title: "No such class session.", detail: $"There is no class session {id}.");
    }
}
