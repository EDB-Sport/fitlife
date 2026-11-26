using FitLife.Authentication;
using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api;

/// <summary>The list of accounts over HTTP (#364). <see cref="ListAccountsHandler"/> does the work.</summary>
[ApiController]
[Route("accounts")]
public sealed class ListAccountsController : ControllerBase
{
    private readonly ListAccountsHandler _handler;

    /// <summary>Creates the controller.</summary>
    /// <param name="handler">Lists accounts.</param>
    public ListAccountsController(ListAccountsHandler handler)
    {
        _handler = handler;
    }

    /// <summary>Lists every account with its id, e-mail address and roles, ordered by e-mail address. Needs a token with the role Admin.</summary>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <response code="200">Every account. Never a password or its hash.</response>
    /// <response code="401">No valid token.</response>
    /// <response code="403">The token doesn't have the role Admin.</response>
    [HttpGet]
    [Authorize(Policy = FitLifePolicies.Admin)]
    [ProducesErrorResponseType(typeof(void))] // 401 and 403 come from the authorization middleware, with no body
    [ProducesResponseType<IReadOnlyList<AccountResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> GetAccounts(CancellationToken cancellationToken)
    {
        IReadOnlyList<Account> accounts = await _handler.HandleAsync(new ListAccountsQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(accounts.Select(AccountResponse.From).ToList());
    }
}
