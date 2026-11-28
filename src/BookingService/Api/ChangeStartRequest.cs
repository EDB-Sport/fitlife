using System.Text.Json.Serialization;

namespace BookingService.Api;

/// <summary>The body of <c>PUT /class-sessions/{id}/start</c> (#241).</summary>
/// <param name="StartsAt">The class session's new start time; after now.</param>
public sealed record ChangeStartRequest([property: JsonRequired] DateTimeOffset StartsAt);
