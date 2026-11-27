namespace BookingService.Application;

/// <summary>A Træner or an Admin cancels a class session (#239): it will not take place.</summary>
/// <param name="ClassSessionId">The class session.</param>
public sealed record CancelClassSessionCommand(Guid ClassSessionId);
