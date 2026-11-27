using BookingService.Application;
using BookingService.Domain;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>Reads and adds through the real repository, but every save loses to a save another request made first.</summary>
internal sealed class StaleSaveRepository(IClassSessionRepository inner) : IClassSessionRepository
{
    public Task<IReadOnlyList<ClassSession>> ListAsync(Guid? centerId, DateTimeOffset startsAfter, CancellationToken cancellationToken)
    {
        return inner.ListAsync(centerId, startsAfter, cancellationToken);
    }

    public Task<ClassSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return inner.GetByIdAsync(id, cancellationToken);
    }

    public Task AddAsync(ClassSession classSession, CancellationToken cancellationToken)
    {
        return inner.AddAsync(classSession, cancellationToken);
    }

    public Task SaveAsync(ClassSession classSession, CancellationToken cancellationToken)
    {
        return Task.FromException(new ConcurrencyConflictException());
    }
}
