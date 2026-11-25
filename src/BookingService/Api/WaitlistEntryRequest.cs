using System.Text.Json.Serialization;

namespace BookingService.Api;

/// <summary>A member asks for a place on the waitlist of a full class session. Until #172, the member id comes in the body.</summary>
/// <param name="MemberId">The member who wants to wait for a seat.</param>
public sealed record WaitlistEntryRequest([property: JsonRequired] Guid MemberId);
