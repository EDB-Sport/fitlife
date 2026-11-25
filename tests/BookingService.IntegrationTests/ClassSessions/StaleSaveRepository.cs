using BookingService.Domain;
using BookingService.Repositories;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>Reads and adds through the real repository, but every save loses to a save another request made first.</summary>
internal sealed class StaleSaveRepository(IClassSessionRepository inner) : IClassSessionRepository
{
    public Task<IReadOnlyList<ClassSession>> ListAsync(Guid? centerId, CancellationToken cancellationToken)
    {
        return inner.ListAsync(centerId, cancellationToken);
    }

    public Task<ClassSession?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return inner.GetAsync(id, cancellationToken);
    }

    public Task AddAsync(ClassSession classSession, CancellationToken cancellationToken)
    {
        return inner.AddAsync(classSession, cancellationToken);
    }

    public Task<bool> SaveAsync(ClassSession classSession, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }
}
