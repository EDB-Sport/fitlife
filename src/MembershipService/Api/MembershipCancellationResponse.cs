namespace MembershipService.Api;

/// <summary>The answer to a cancellation (UC8): the last day the membership gives access.</summary>
/// <param name="LastTrainingDay">The last day of the month after the month the member cancelled in.</param>
public sealed record MembershipCancellationResponse(DateOnly LastTrainingDay);
