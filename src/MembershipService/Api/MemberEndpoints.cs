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
    /// Maps <c>POST /members</c>, <c>GET /members/me</c>, <c>POST /members/me/payment</c> and <c>GET /members/by-account/{accountId}</c>.
    /// All need a token from Identity.
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
        ClaimsPrincipal user, JoinMembershipHandler handler, CancellationToken cancellationToken)
    {
        string accountId = user.GetAccountId();
        JoinResult result = await handler.HandleAsync(new JoinMembershipCommand(accountId), cancellationToken).ConfigureAwait(false);
        if (result.Member is null || result.Membership is null)
        {
            return Conflict("The account is already a member", "AccountIsAlreadyAMember");
        }

        var response = new MemberResponse(result.Member.Id, result.Membership.Status, result.Membership.IsActive);
        return TypedResults.Created(new Uri($"/members/by-account/{Uri.EscapeDataString(accountId)}", UriKind.Relative), response);
    }

    private static async Task<Results<Ok<MyMembershipResponse>, NotFound>> GetMine(
        ClaimsPrincipal user, IMemberRepository members, IMembershipRepository memberships, CancellationToken cancellationToken)
    {
        // The account comes from the token, never from the path, so a member only ever sees their own (ADR 007)
        MyMembershipResponse? mine = await FindMine(user.GetAccountId(), members, memberships, cancellationToken).ConfigureAwait(false);
        if (mine is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(mine);
    }

    private static async Task<Results<Ok<MyMembershipResponse>, NotFound, ProblemHttpResult>> Pay(
        ClaimsPrincipal user,
        PayMembershipHandler handler,
        IMemberRepository members,
        IMembershipRepository memberships,
        CancellationToken cancellationToken)
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
        MyMembershipResponse? mine = await FindMine(accountId, members, memberships, cancellationToken).ConfigureAwait(false);
        if (mine is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(mine);
    }

    private static ProblemHttpResult Conflict(string title, string code)
    {
        return TypedResults.Problem(
            title: title,
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code });
    }

    private static async Task<MyMembershipResponse?> FindMine(
        string accountId, IMemberRepository members, IMembershipRepository memberships, CancellationToken cancellationToken)
    {
        Member? member = await members.FindByAccountIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return null;
        }

        Membership? membership = await memberships.FindByMemberIdAsync(member.Id, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return null;
        }

        return new MyMembershipResponse(member.Id, membership.Type, membership.Status, membership.IsActive, membership.StartDate);
    }

    private static async Task<Results<Ok<MemberResponse>, NotFound>> GetByAccount(
        string accountId, ClaimsPrincipal user, IMemberRepository members, IMembershipRepository memberships, CancellationToken cancellationToken)
    {
        // A member looks up only their own account. Another account answers as if it were unknown, so the lookup doesn't tell
        // which accounts are members (review on #199).
        if (!string.Equals(user.GetAccountId(), accountId, StringComparison.Ordinal))
        {
            return TypedResults.NotFound();
        }

        Member? member = await members.FindByAccountIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return TypedResults.NotFound();
        }

        Membership? membership = await memberships.FindByMemberIdAsync(member.Id, cancellationToken).ConfigureAwait(false);
        if (membership is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new MemberResponse(member.Id, membership.Status, membership.IsActive));
    }
}
