using System.Security.Claims;
using FitLife.Authentication;
using MembershipService.Application;
using MembershipService.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MembershipService.Api;

/// <summary>Joining FitLife, and the lookup by account id other services use (CONTEXT-MAP section 4, Open Host Service).</summary>
public static class MemberEndpoints
{
    /// <summary>
    /// Maps <c>POST /members</c>, <c>GET /members/me</c>, <c>POST /members/me/payment</c>, <c>GET /members/by-account/{accountId}</c>,
    /// <c>PUT</c> and <c>DELETE /members/me/pause</c>, and <c>POST /members/me/cancellation</c>. All need a token from Identity. A status is the status on today, the UTC date.
    /// </summary>
    /// <param name="app">The routes.</param>
    /// <returns>The routes.</returns>
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder members = app.MapGroup("/members").RequireAuthorization().WithTags("Members")
            .AddEndpointFilter(RequireAccountId);
        members.MapPost("/", Join)
            .WithName("JoinMembership")
            .WithSummary("The account in the token joins FitLife and gets an active membership")
            .ProducesProblem(StatusCodes.Status409Conflict);
        members.MapGet("/by-account/{accountId}", GetByAccount)
            .WithName("GetMemberByAccount")
            .WithSummary("The member id of an account, and whether its membership is active");
        members.MapGet("/me", GetMine)
            .WithName("GetMyMembership")
            .WithSummary("The membership of the account in the token: type, status and start date");
        members.MapPost("/me/payment", Pay)
            .WithName("PayMembership")
            .WithSummary("The account in the token pays the missing fee of a membership with Betaling mangler, which becomes Aktiv")
            .ProducesProblem(StatusCodes.Status409Conflict);
        members.MapPut("/me/pause", Pause)
            .WithName("PauseMyMembership")
            .WithSummary("Pauses the membership of the account in the token from one day to another, both included")
            .ProducesProblem(StatusCodes.Status409Conflict);
        members.MapDelete("/me/pause", EndPause)
            .WithName("EndMyPause")
            .WithSummary("Ends the pause of the membership of the account in the token: it is active from today")
            .ProducesProblem(StatusCodes.Status409Conflict);
        members.MapPost("/me/cancellation", Cancel)
            .WithName("CancelMyMembership")
            .WithSummary("Cancels the membership of the account in the token with one month's notice: it gives access until the last day of the next month")
            .ProducesProblem(StatusCodes.Status409Conflict);
        return app;
    }

    // Every route here reads the account id. A valid token without one (no `sub`) is answered 401, as before #368,
    // so GetAccountId() never throws in a route
    private static async ValueTask<object?> RequireAccountId(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (string.IsNullOrWhiteSpace(context.HttpContext.User.FindFirstValue(FitLifeJwt.AccountIdClaim)))
        {
            return TypedResults.Unauthorized();
        }

        return await next(context).ConfigureAwait(false);
    }

    private static async Task<Results<Created<MemberResponse>, ProblemHttpResult>> Join(
        ClaimsPrincipal user, JoinMembershipHandler handler, TimeProvider clock, CancellationToken cancellationToken)
    {
        string accountId = user.GetAccountId();
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand(accountId), cancellationToken).ConfigureAwait(false);
        if (result.Member is null || result.Membership is null)
        {
            return Conflict("The account is already a member", "AccountIsAlreadyAMember");
        }

        DateOnly today = MembershipLookup.Today(clock);
        var response = new MemberResponse(result.Member.Id, result.Membership.StatusOn(today), result.Membership.IsActiveOn(today));
        return TypedResults.Created(new Uri($"/members/by-account/{Uri.EscapeDataString(accountId)}", UriKind.Relative), response);
    }

    private static async Task<Results<Ok<MyMembershipResponse>, NotFound>> GetMine(
        ClaimsPrincipal user, [AsParameters] MembershipReaders readers, CancellationToken cancellationToken)
    {
        // The account comes from the token, never from the path, so a member only ever sees their own (ADR 007)
        MyMembershipResponse? mine = await FindMine(user.GetAccountId(), readers, cancellationToken).ConfigureAwait(false);
        if (mine is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(mine);
    }

    private static async Task<Results<Ok<MyMembershipResponse>, NotFound, ProblemHttpResult>> Pay(
        ClaimsPrincipal user, PayMembershipHandler handler, [AsParameters] MembershipReaders readers, CancellationToken cancellationToken)
    {
        string accountId = user.GetAccountId();
        PayMembershipResult result = await handler.HandleAsync(new PayMembershipCommand(accountId), cancellationToken).ConfigureAwait(false);
        switch (result)
        {
            case PayMembershipResult.NotAMember:
                return TypedResults.NotFound();
            case PayMembershipResult.NothingIsOwed:
                return Conflict("The membership owes no fee", nameof(PayMembershipResult.NothingIsOwed));
            case PayMembershipResult.ChargeDeclined:
                return Conflict("The payment provider did not accept the charge", nameof(PayMembershipResult.ChargeDeclined));
            default:
                break;
        }

        // Paid: the answer is the body of GET /members/me
        MyMembershipResponse? mine = await FindMine(accountId, readers, cancellationToken).ConfigureAwait(false);
        if (mine is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(mine);
    }

    private static async Task<Results<Ok<MembershipPauseResponse>, NotFound, ProblemHttpResult, ValidationProblem>> Pause(
        PauseRequest request, ClaimsPrincipal user, PauseMembershipHandler handler, TimeProvider clock, CancellationToken cancellationToken)
    {
        // A malformed date never reaches here: the body doesn't bind, which is a 400. A missing one is a 400 too
        if (request.From is not { } from || request.To is not { } to)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["from"] = ["Both from and to are needed, as dates like 2026-12-01."],
                ["to"] = ["Both from and to are needed, as dates like 2026-12-01."],
            });
        }

        MembershipCommandResult<PauseResult> result = await handler.HandleAsync(
            new PauseMembershipCommand(user.GetAccountId(), from, to), cancellationToken).ConfigureAwait(false);
        if (result.Membership is null)
        {
            return TypedResults.NotFound();
        }

        if (result.Result != PauseResult.Paused)
        {
            return Conflict("The membership can't be paused for that period", result.Result.ToString());
        }

        return TypedResults.Ok(PauseResponse(result.Membership, MembershipLookup.Today(clock)));
    }

    private static async Task<Results<Ok<MembershipPauseResponse>, NotFound, ProblemHttpResult>> EndPause(
        ClaimsPrincipal user, EndPauseHandler handler, TimeProvider clock, CancellationToken cancellationToken)
    {
        MembershipCommandResult<EndPauseResult> result = await handler.HandleAsync(
            new EndPauseCommand(user.GetAccountId()), cancellationToken).ConfigureAwait(false);
        if (result.Membership is null)
        {
            return TypedResults.NotFound();
        }

        if (result.Result != EndPauseResult.PauseEnded)
        {
            return Conflict("The membership has no pause to end", result.Result.ToString());
        }

        return TypedResults.Ok(PauseResponse(result.Membership, MembershipLookup.Today(clock)));
    }

    private static async Task<Results<Ok<MembershipCancellationResponse>, NotFound, ProblemHttpResult>> Cancel(
        ClaimsPrincipal user, CancelMembershipHandler handler, CancellationToken cancellationToken)
    {
        MembershipCommandResult<CancelResult> result = await handler.HandleAsync(
            new CancelMembershipCommand(user.GetAccountId()), cancellationToken).ConfigureAwait(false);
        if (result.Membership is null)
        {
            return TypedResults.NotFound();
        }

        if (result.Result != CancelResult.Cancelled || result.Membership.LastTrainingDay is not { } lastTrainingDay)
        {
            return Conflict("The membership is already cancelled", nameof(CancelResult.MembershipAlreadyCancelled));
        }

        return TypedResults.Ok(new MembershipCancellationResponse(lastTrainingDay));
    }

    // 409 problem details whose extension "code" names what was rejected, e.g. "PauseStartsInThePast"
    private static ProblemHttpResult Conflict(string title, string code)
    {
        return TypedResults.Problem(
            title: title,
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code });
    }

    private static MembershipPauseResponse PauseResponse(Membership membership, DateOnly today)
    {
        return new MembershipPauseResponse(
            membership.MemberId, membership.Type, membership.StatusOn(today), membership.IsActiveOn(today), membership.StartDate, membership.PausedFrom, membership.PausedTo);
    }

    private static async Task<MyMembershipResponse?> FindMine(string accountId, MembershipReaders readers, CancellationToken cancellationToken)
    {
        Member? member = await readers.Members.FindByAccountIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return null;
        }

        Membership? membership = await readers.Memberships.FindByMemberIdAsync(member.Id, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return null;
        }

        DateOnly today = MembershipLookup.Today(readers.Clock);
        return new MyMembershipResponse(member.Id, membership.Type, membership.StatusOn(today), membership.IsActiveOn(today), membership.StartDate, membership.LastTrainingDay);
    }

    private static async Task<Results<Ok<MemberResponse>, NotFound>> GetByAccount(
        string accountId, ClaimsPrincipal user, [AsParameters] MembershipReaders readers, CancellationToken cancellationToken)
    {
        // A member looks up only their own account. Another account answers as if it were unknown, so the lookup doesn't tell
        // which accounts are members (review on #199).
        if (!string.Equals(user.GetAccountId(), accountId, StringComparison.Ordinal))
        {
            return TypedResults.NotFound();
        }

        Member? member = await readers.Members.FindByAccountIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return TypedResults.NotFound();
        }

        Membership? membership = await readers.Memberships.FindByMemberIdAsync(member.Id, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return TypedResults.NotFound();
        }

        DateOnly today = MembershipLookup.Today(readers.Clock);
        return TypedResults.Ok(new MemberResponse(member.Id, membership.StatusOn(today), membership.IsActiveOn(today)));
    }
}
