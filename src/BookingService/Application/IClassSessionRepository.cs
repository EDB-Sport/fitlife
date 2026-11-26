using BookingService.Domain;

namespace BookingService.Application;

/// <summary>The one repository of the <see cref="ClassSession"/> aggregate (METHODOLOGY §11.2 rule 4). The MongoDB class is <c>Repositories/ClassSessionRepository</c>.</summary>
public interface IClassSessionRepository
{
    /// <summary>The class sessions that start after a moment, by start time, of one centre or, without a centre, of all of them.</summary>
    /// <param name="centerId">The centre, or null for every centre.</param>
    /// <param name="startsAfter">Only class sessions that start later than this.</param>
    /// <param name="cancellationToken">Stops the query.</param>
    Task<IReadOnlyList<ClassSession>> ListAsync(Guid? centerId, DateTimeOffset startsAfter, CancellationToken cancellationToken);

    /// <summary>One class session, or null when there is none with that id.</summary>
    /// <param name="id">The class session's id.</param>
    /// <param name="cancellationToken">Stops the query.</param>
    Task<ClassSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Stores a new class session.</summary>
    /// <param name="classSession">The class session.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    Task AddAsync(ClassSession classSession, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the changes to a class session read with <see cref="GetByIdAsync"/>. When another request saved the class session
    /// in the meantime, nothing is stored and <see cref="ConcurrencyConflictException"/> is thrown: then the caller reads it again.
    /// </summary>
    /// <param name="classSession">The changed class session.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    Task SaveAsync(ClassSession classSession, CancellationToken cancellationToken);
}
