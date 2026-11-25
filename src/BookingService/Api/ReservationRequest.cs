using System.Text.Json.Serialization;

namespace BookingService.Api;

/// <summary>A member asks for a seat. Until the JWT issue (#172), the member id comes in the body.</summary>
/// <param name="MemberId">The member who wants the seat.</param>
public sealed record ReservationRequest([property: JsonRequired] Guid MemberId);
