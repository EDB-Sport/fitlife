using System.Diagnostics;
using FitLife.Authentication;
using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api;

/// <summary>
/// An Admin gives or takes the role Træner (#365). Both calls are idempotent. The path says <c>traener</c>; the role's value in
/// the token stays "Træner". <see cref="ChangeRoleHandler"/> does the work; this class only turns its answer into a status code.
/// </summary>
[ApiController]
[Route("accounts/{id:guid}/roles/traener")]
[Authorize(Policy = FitLifePolicies.Admin)]
public sealed class AccountRolesController : ControllerBase
{
    private readonly ChangeRoleHandler _handler;

    /// <summary>Creates the controller.</summary>
    /// <param name="handler">Gives and takes roles.</param>
    public AccountRolesController(ChangeRoleHandler handler)
    {
        _handler = handler;
    }

    /// <summary>Gives the account the role Træner. It applies from the account's next log-in.</summary>
    /// <param name="id">The account.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <response code="204">The account has the role Træner, also when it had it already.</response>
    /// <response code="404">No account has the id.</response>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<Results<NoContent, ProblemHttpResult>> PutTraener(Guid id, CancellationToken cancellationToken)
    {
        ChangeRoleOutcome outcome = await _handler.HandleAsync(new GiveRoleCommand(id, Role.Traener, User.GetAccountId()), cancellationToken).ConfigureAwait(false);
        return StatusCodeFor(id, outcome);
    }

    /// <summary>Takes the role Træner from the account. It applies from the account's next log-in.</summary>
    /// <param name="id">The account.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <response code="204">The account doesn't have the role Træner, also when it never had it.</response>
    /// <response code="404">No account has the id.</response>
    /// <response code="409">Træner is the account's only role, and an account always keeps one.</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<Results<NoContent, ProblemHttpResult>> DeleteTraener(Guid id, CancellationToken cancellationToken)
    {
        ChangeRoleOutcome outcome = await _handler.HandleAsync(new TakeRoleCommand(id, Role.Traener, User.GetAccountId()), cancellationToken).ConfigureAwait(false);
        return StatusCodeFor(id, outcome);
    }

    private static Results<NoContent, ProblemHttpResult> StatusCodeFor(Guid id, ChangeRoleOutcome outcome)
    {
        switch (outcome)
        {
            case ChangeRoleOutcome.Changed:
            case ChangeRoleOutcome.NothingToChange:
                return TypedResults.NoContent();
            case ChangeRoleOutcome.AccountNotFound:
                return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "No such account.", detail: $"There is no account {id}.");
            case ChangeRoleOutcome.LastRoleKept:
                return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Træner is the account's only role. An account always keeps one.");
            default:
                throw new UnreachableException($"ChangeRoleOutcome.{outcome} has no status code.");
        }
    }
}
