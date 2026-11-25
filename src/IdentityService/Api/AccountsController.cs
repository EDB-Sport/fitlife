using System.Diagnostics;
using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api;

/// <summary>Accounts over HTTP (#234). <see cref="CreateAccountHandler"/> does the work; this class only turns its answer into a status code.</summary>
[ApiController]
[Route("accounts")]
public sealed class AccountsController : ControllerBase
{
    private readonly CreateAccountHandler _handler;

    /// <summary>Creates the controller.</summary>
    /// <param name="handler">Creates accounts.</param>
    public AccountsController(CreateAccountHandler handler)
    {
        _handler = handler;
    }

    /// <summary>Creates an account with the role Medlem. No login needed.</summary>
    /// <param name="request">The e-mail address and the password.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <response code="201">The account is created.</response>
    /// <response code="400">The e-mail address isn't an address, or the password is shorter than 8 characters.</response>
    /// <response code="409">Another account has the e-mail address, in some letter case. The problem's <c>code</c> is <c>EmailAlreadyTaken</c>.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AccountResponse>> PostAccount(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Stryker disable once Boolean : ConfigureAwait(false) -> true is an equivalent mutant (#270)
        CreateAccountOutcome outcome = await _handler.HandleAsync(new CreateAccountCommand(request.Email, request.Password), cancellationToken).ConfigureAwait(false);
        switch (outcome.Result)
        {
            case CreateAccountResult.Created when outcome.Account is Account account:
                return StatusCode(StatusCodes.Status201Created, new AccountResponse(account.Id, account.Email, [.. account.Roles.Select(role => role.Name)]));
            case CreateAccountResult.EmailAlreadyTaken:
                ProblemDetails problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status409Conflict, "The e-mail address is taken.");
                problem.Extensions["code"] = nameof(CreateAccountResult.EmailAlreadyTaken);
                return Conflict(problem);
            default:
                throw new UnreachableException($"CreateAccountResult.{outcome.Result} has no status code.");
        }
    }
}
