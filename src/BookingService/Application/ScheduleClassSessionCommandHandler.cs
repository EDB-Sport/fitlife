using BookingService.Domain;
using BookingService.Models;

namespace BookingService.Application;

/// <summary>
/// Handles <see cref="ScheduleClassSessionCommand"/> (#238): checks the values and that the centre and class type exist, then saves
/// a new <see cref="ClassSession"/>. It doesn't check whether class sessions overlap.
/// </summary>
public sealed class ScheduleClassSessionCommandHandler
{
    private readonly IClassSessionRepository _classSessions;
    private readonly IReferenceDataRepository _referenceData;
    private readonly TimeProvider _clock;

    /// <summary>Creates the handler.</summary>
    /// <param name="classSessions">Where class sessions are stored.</param>
    /// <param name="referenceData">The centres and class types.</param>
    /// <param name="clock">Tells the time now.</param>
    public ScheduleClassSessionCommandHandler(IClassSessionRepository classSessions, IReferenceDataRepository referenceData, TimeProvider clock)
    {
        _classSessions = classSessions;
        _referenceData = referenceData;
        _clock = clock;
    }

    /// <summary>Schedules the class session.</summary>
    /// <param name="command">Where, what, with whom, when, how long and how many seats.</param>
    /// <param name="cancellationToken">Stops the calls.</param>
    /// <returns>The new class session, or why nothing was saved.</returns>
    public async Task<ScheduleClassSessionResult> Handle(ScheduleClassSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.StartsAt <= _clock.GetUtcNow())
        {
            return Rejected(ScheduleClassSessionOutcome.StartNotInTheFuture);
        }

        if (command.Capacity < 1)
        {
            return Rejected(ScheduleClassSessionOutcome.CapacityUnderOne);
        }

        if (command.DurationMinutes < 1)
        {
            return Rejected(ScheduleClassSessionOutcome.DurationUnderOneMinute);
        }

        if (string.IsNullOrWhiteSpace(command.InstructorName))
        {
            return Rejected(ScheduleClassSessionOutcome.InstructorNameMissing);
        }

        IReadOnlyList<Center> centers = await _referenceData.ListCentersAsync(cancellationToken).ConfigureAwait(false);
        Center? center = centers.FirstOrDefault(candidate => candidate.Id == command.CenterId);
        if (center is null)
        {
            return Rejected(ScheduleClassSessionOutcome.CenterNotFound);
        }

        IReadOnlyList<ClassType> classTypes = await _referenceData.ListClassTypesAsync(cancellationToken).ConfigureAwait(false);
        ClassType? classType = classTypes.FirstOrDefault(candidate => candidate.Id == command.ClassTypeId);
        if (classType is null)
        {
            return Rejected(ScheduleClassSessionOutcome.ClassTypeNotFound);
        }

        var details = new ClassSessionDetails(center.Id, classType.Id, new Instructor(command.InstructorName), TimeSpan.FromMinutes(command.DurationMinutes));
        var classSession = new ClassSession(Guid.NewGuid(), new Capacity(command.Capacity), command.StartsAt, details);
        await _classSessions.AddAsync(classSession, cancellationToken).ConfigureAwait(false);
        return new ScheduleClassSessionResult(ScheduleClassSessionOutcome.Scheduled, ClassSessionSummary.Of(classSession, center.Name, classType.Name));
    }

    private static ScheduleClassSessionResult Rejected(ScheduleClassSessionOutcome outcome)
    {
        return new ScheduleClassSessionResult(outcome, ClassSession: null);
    }
}
