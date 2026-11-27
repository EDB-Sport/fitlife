namespace BookingService.Application;

/// <summary>What <see cref="ScheduleClassSessionCommandHandler"/> answered (#238).</summary>
/// <param name="Outcome">Whether the class session was scheduled, and if not, why.</param>
/// <param name="ClassSession">The new class session as the class schedule shows it, only when <see cref="ScheduleClassSessionOutcome.Scheduled"/>.</param>
public sealed record ScheduleClassSessionResult(ScheduleClassSessionOutcome Outcome, ClassSessionSummary? ClassSession);
