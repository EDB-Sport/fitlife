using IdentityService.Application;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api;

/// <summary>Tokens over HTTP (#235). <see cref="LogInHandler"/> does the work; this class only turns its answer into a status code.</summary>
[ApiController]
[Route("tokens")]
public sealed class TokensController : ControllerBase
{
    private readonly LogInHandler _handler;

    /// <summary>Creates the controller.</summary>
    /// <param name="handler">Logs in.</param>
    public TokensController(LogInHandler handler)
    {
        _handler = handler;
    }

    /// <summary>Logs in with an e-mail address and a password, and gives a token with the account's id and roles. No token needed.</summary>
    /// <param name="request">The e-mail address and the password.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <response code="200">The token, valid for 60 minutes.</response>
    /// <response code="400">The e-mail address or the password is missing.</response>
    /// <response code="401">No account has the e-mail address, or the password is wrong. Both give the same answer.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<TokenResponse>> PostToken(LogInRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        LogInResult? result = await _handler.HandleAsync(new LogInCommand(request.Email, request.Password), cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            // Built here, not by ProblemDetailsFactory: its per-request traceId would make the two rejections differ
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "The e-mail address or the password is wrong.",
            });
        }

        return Ok(new TokenResponse(result.AccessToken, result.ExpiresAt));
    }
}
