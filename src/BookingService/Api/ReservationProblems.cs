using System.Diagnostics;
using BookingService.Domain;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.Api;

/// <summary>
/// The problem details for a rejected reservation, in one place the later endpoints reuse (#260). Every one carries a <c>code</c>;
/// a rejection by the class session is 409 with <c>code</c> = the <see cref="ReservationResult"/> member's name.
/// </summary>
internal static class ReservationProblems
{
    /// <summary>The problem-details extension a client switches on.</summary>
    public const string CodeExtension = "code";

    /// <summary>A rejection by the class session: 409 with the result's name as its code.</summary>
    /// <param name="controller">The controller that answers.</param>
    /// <param name="result">What the class session said; never <see cref="ReservationResult.Made"/>.</param>
    /// <returns>The problem.</returns>
    public static ObjectResult Rejected(ControllerBase controller, ReservationResult result)
    {
        return Create(controller, StatusCodes.Status409Conflict, TitleOf(result), result.ToString());
    }

    /// <summary>A problem with its status, title and code.</summary>
    /// <param name="controller">The controller that answers.</param>
    /// <param name="status">The status code.</param>
    /// <param name="title">What went wrong, for a person.</param>
    /// <param name="code">What went wrong, for a client.</param>
    /// <returns>The problem.</returns>
    public static ObjectResult Create(ControllerBase controller, int status, string title, string code)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ProblemDetails problem = controller.ProblemDetailsFactory.CreateProblemDetails(controller.HttpContext, status, title);
        problem.Extensions[CodeExtension] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }

    private static string TitleOf(ReservationResult result)
    {
        switch (result)
        {
            case ReservationResult.TooFarAhead:
                return "The class session starts more than 4 weeks from now.";
            case ReservationResult.ClassSessionFull:
                return "The class session is full. Join its waitlist instead.";
            case ReservationResult.MemberAlreadyHasReservation:
                return "The member already has a seat on this class session.";
            case ReservationResult.ClassSessionCancelled:
                return "The class session is cancelled.";
            case ReservationResult.ClassSessionStarted:
                return "The class session has started.";
            default:
                throw new UnreachableException($"ReservationResult.{result} is not a rejection with a title.");
        }
    }
}
