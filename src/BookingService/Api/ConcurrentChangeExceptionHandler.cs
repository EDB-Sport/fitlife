using BookingService.Application;
using Microsoft.AspNetCore.Diagnostics;

namespace BookingService.Api;

/// <summary>
/// The one mapping of a refused save to HTTP (#245): 409 problem details with <c>code: ConcurrentChange</c>, never a 500. Any
/// endpoint that lets <see cref="ConcurrencyConflictException"/> through gets it.
/// </summary>
internal sealed class ConcurrentChangeExceptionHandler : IExceptionHandler
{
    /// <summary>What went wrong, for a person.</summary>
    public const string Title = "The class session changed at the same time. Try again.";

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ConcurrencyConflictException)
        {
            return false;
        }

        var code = new Dictionary<string, object?>(StringComparer.Ordinal) { [ReservationProblems.CodeExtension] = nameof(ReserveSeatOutcome.ConcurrentChange) };
        await TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: Title, extensions: code)
            .ExecuteAsync(httpContext)
            .ConfigureAwait(false);
        return true;
    }
}
