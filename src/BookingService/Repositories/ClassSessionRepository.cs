using BookingService.Application;
using BookingService.Domain;
using MongoDB.Driver;

namespace BookingService.Repositories;

/// <summary>
/// Stores <see cref="ClassSession"/> aggregates in the <c>classSessions</c> collection. One instance per request: it remembers the
/// version of each class session it read, so <see cref="SaveAsync"/> can refuse a save on a stale copy.
/// </summary>
public sealed class ClassSessionRepository : IClassSessionRepository
{
    private readonly IMongoCollection<ClassSessionDocument> _collection;
    private readonly Dictionary<Guid, int> _versions = [];

    /// <summary>Creates the repository on the service's own database.</summary>
    /// <param name="database">The Booking database.</param>
    public ClassSessionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ClassSessionDocument>("classSessions");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClassSession>> ListAsync(Guid? centerId, CancellationToken cancellationToken)
    {
        FilterDefinition<ClassSessionDocument> filter = Builders<ClassSessionDocument>.Filter.Empty;
        if (centerId is { } id)
        {
            filter = Builders<ClassSessionDocument>.Filter.Eq(document => document.CenterId, id);
        }

        List<ClassSessionDocument> documents = await _collection.Find(filter)
            .SortBy(document => document.StartTime)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return documents.ConvertAll(Read);
    }

    /// <inheritdoc />
    public async Task<ClassSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        ClassSessionDocument? document = await _collection.Find(candidate => candidate.Id == id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        return Read(document);
    }

    /// <inheritdoc />
    public async Task AddAsync(ClassSession classSession, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(classSession);
        await _collection.InsertOneAsync(Write(classSession, version: 1), cancellationToken: cancellationToken).ConfigureAwait(false);
        _versions[classSession.Id] = 1;
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(ClassSession classSession, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(classSession);
        int version = _versions[classSession.Id];
        ReplaceOneResult result = await _collection.ReplaceOneAsync(
                candidate => candidate.Id == classSession.Id && candidate.Version == version,
                Write(classSession, version + 1),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (result.ModifiedCount == 0)
        {
            return false;
        }

        _versions[classSession.Id] = version + 1;
        return true;
    }

    private ClassSession Read(ClassSessionDocument document)
    {
        _versions[document.Id] = document.Version;
        var classSession = new ClassSession(
            document.Id,
            new Capacity(document.Capacity),
            new DateTimeOffset(DateTime.SpecifyKind(document.StartTime, DateTimeKind.Utc)),
            new ClassSessionDetails(document.CenterId, document.ClassTypeId, new Instructor(document.InstructorName), document.Duration));
        classSession.Restore(
            document.IsCancelled,
            document.Reservations.Select(ReadReservation),
            document.Waitlist.Select(memberId => new WaitlistEntry(memberId)));
        return classSession;
    }

    private static Reservation ReadReservation(ReservationDocument document)
    {
        var reservation = new Reservation(document.MemberId, document.IsFromWaitlist);
        if (document.IsFromBeforeAChangedStart)
        {
            return reservation.FromBeforeAChangedStart();
        }

        return reservation;
    }

    private static ClassSessionDocument Write(ClassSession classSession, int version)
    {
        return new ClassSessionDocument
        {
            Id = classSession.Id,
            CenterId = classSession.CenterId,
            ClassTypeId = classSession.ClassTypeId,
            InstructorName = classSession.Details.Instructor.Name,
            Duration = classSession.Details.Duration,
            Capacity = classSession.Capacity.Value,
            StartTime = classSession.StartTime.UtcDateTime,
            IsCancelled = classSession.IsCancelled,
            Reservations = classSession.Reservations
                .Select(reservation => new ReservationDocument
                {
                    MemberId = reservation.MemberId,
                    IsFromWaitlist = reservation.IsFromWaitlist,
                    IsFromBeforeAChangedStart = reservation.IsFromBeforeAChangedStart,
                })
                .ToList(),
            Waitlist = classSession.Waitlist.Select(waitlistEntry => waitlistEntry.MemberId).ToList(),
            Version = version,
        };
    }
}
