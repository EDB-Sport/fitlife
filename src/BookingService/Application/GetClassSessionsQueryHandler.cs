using BookingService.Domain;

namespace BookingService.Application;

/// <summary>Answers <see cref="GetClassSessionsQuery"/>: the upcoming class sessions, by start, with the names of their centre and class type.</summary>
public sealed class GetClassSessionsQueryHandler
{
    private readonly IClassSessionRepository _classSessions;
    private readonly IReferenceDataRepository _referenceData;
    private readonly TimeProvider _clock;

    /// <summary>Creates the handler.</summary>
    /// <param name="classSessions">Where class sessions are stored.</param>
    /// <param name="referenceData">The centres and class types.</param>
    /// <param name="clock">Tells the time now.</param>
    public GetClassSessionsQueryHandler(IClassSessionRepository classSessions, IReferenceDataRepository referenceData, TimeProvider clock)
    {
        _classSessions = classSessions;
        _referenceData = referenceData;
        _clock = clock;
    }

    /// <summary>The class schedule.</summary>
    /// <param name="query">Which centre.</param>
    /// <param name="cancellationToken">Stops the queries.</param>
    public async Task<IReadOnlyList<ClassSessionSummary>> Handle(GetClassSessionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IReadOnlyList<ClassSession> classSessions = await _classSessions.ListAsync(query.CenterId, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var centers = (await _referenceData.ListCentersAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(center => center.Id, center => center.Name);
        var classTypes = (await _referenceData.ListClassTypesAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(classType => classType.Id, classType => classType.Name);
        return classSessions.Select(classSession => ClassSessionSummary.Of(
            classSession,
            centers.GetValueOrDefault(classSession.CenterId, ""),
            classTypes.GetValueOrDefault(classSession.ClassTypeId, ""))).ToList();
    }
}
