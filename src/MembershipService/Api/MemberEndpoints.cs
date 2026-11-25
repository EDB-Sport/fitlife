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
    /// Maps <c>POST /members</c>, <c>GET /members/me</c> and <c>GET /members/by-account/{accountId}</c>. All need a token from Identity.
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
            return TypedResults.Problem(title: "The account is already a member", statusCode: StatusCodes.Status409Conflict);
        }

        var response = new MemberResponse(result.Member.Id, result.Membership.Status, result.Membership.IsActive);
        return TypedResults.Created(new Uri($"/members/by-account/{Uri.EscapeDataString(accountId)}", UriKind.Relative), response);
    }

    private static async Task<Results<Ok<MyMembershipResponse>, NotFound>> GetMine(
        ClaimsPrincipal user, IMemberRepository members, IMembershipRepository memberships, CancellationToken cancellationToken)
    {
        // The account comes from the token, never from the path, so a member only ever sees their own (ADR 007)
        string accountId = user.GetAccountId();

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

        return TypedResults.Ok(new MyMembershipResponse(member.Id, membership.Type, membership.Status, membership.IsActive, membership.StartDate));
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
