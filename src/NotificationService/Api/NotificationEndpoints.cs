using FitLife.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using NotificationService.Models;
using NotificationService.Repositories;

namespace NotificationService.Api;

/// <summary>The delivery log over HTTP: which notifications a member has got (#221).</summary>
public static class NotificationEndpoints
{
    /// <summary>Maps <c>GET /notifications?memberId={id}</c>, for the roles Træner and Admin (docs/domain/ACCESS.md).</summary>
    /// <param name="app">The routes.</param>
    /// <returns>The routes.</returns>
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications", List)
            .RequireAuthorization(FitLifePolicies.TraenerOrAdmin)
            .WithTags("Notifications")
            .WithName("ListNotifications")
            .WithSummary("The notifications a member has got, newest first. For staff: Træner and Admin")
            .ProducesProblem(StatusCodes.Status400BadRequest);
        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<NotificationResponse>>, ProblemHttpResult>> List(
        string? memberId, INotificationRepository notifications, CancellationToken cancellationToken)
    {
        // Text, not a Guid: the events carry member ids as strings (contracts/events/reservation-made)
        if (string.IsNullOrEmpty(memberId))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Give the member id: /notifications?memberId={id}.");
        }

        IReadOnlyList<Notification> listed = await notifications.ListForMemberAsync(memberId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<NotificationResponse> response =
            [.. listed.Select(notification => new NotificationResponse(notification.Id, notification.MemberId, notification.Kind, notification.Text, notification.OccurredAt))];
        return TypedResults.Ok(response);
    }
}
