using BookingService.Domain;

namespace BookingService.Application;

/// <summary>What <see cref="ReserveSeatCommandHandler"/> answered (#260).</summary>
/// <param name="Outcome">How far it got.</param>
/// <param name="Reservation">The class session's answer, when <see cref="ReserveSeatOutcome.Answered"/>.</param>
/// <param name="MemberId">The member Membership named, or <see cref="Guid.Empty"/> before Membership answered with one.</param>
public sealed record ReserveSeatResult(ReserveSeatOutcome Outcome, ReservationResult Reservation, Guid MemberId);
