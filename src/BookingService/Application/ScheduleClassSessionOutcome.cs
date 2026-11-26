namespace BookingService.Application;

/// <summary>What <see cref="ScheduleClassSessionCommandHandler"/> did (#238). Every outcome but the first saves nothing.</summary>
public enum ScheduleClassSessionOutcome
{
    /// <summary>The class session is saved.</summary>
    Scheduled,

    /// <summary>The start time is not after now.</summary>
    StartNotInTheFuture,

    /// <summary>The capacity is under 1.</summary>
    CapacityUnderOne,

    /// <summary>The duration is under 1 minute.</summary>
    DurationUnderOneMinute,

    /// <summary>The instructor's name is empty or white space.</summary>
    InstructorNameMissing,

    /// <summary>The centre is not in the reference data.</summary>
    CenterNotFound,

    /// <summary>The class type is not in the reference data.</summary>
    ClassTypeNotFound,
}
