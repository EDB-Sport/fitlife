namespace BookingService.Application;

/// <summary>A Træner or an Admin moves a class session to a new start time (#241).</summary>
/// <param name="ClassSessionId">The class session.</param>
/// <param name="NewStart">When it starts from now on.</param>
public sealed record ChangeClassSessionStartCommand(Guid ClassSessionId, DateTimeOffset NewStart);
