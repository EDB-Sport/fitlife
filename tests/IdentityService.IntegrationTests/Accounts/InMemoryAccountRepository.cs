using System.Collections.Concurrent;
using IdentityService.Application;
using IdentityService.Domain;

namespace IdentityService.IntegrationTests.Accounts;

/// <summary>Accounts in memory, so the endpoint tests need no MongoDB. Letter case doesn't matter, as in MongoAccountRepository.</summary>
internal sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<string, Account> _accounts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The stored accounts.</summary>
    public ICollection<Account> Accounts => _accounts.Values;

    public Task<bool> AddAsync(Account account, CancellationToken cancellationToken)
    {
        return Task.FromResult(_accounts.TryAdd(account.Email, account));
    }

    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return Task.FromResult(_accounts.GetValueOrDefault(email));
    }

    public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Account>>([.. _accounts.Values]);
    }

    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_accounts.Values.FirstOrDefault(account => account.Id == id));
    }

    // The stored account is the same object the handler changed, so its roles are already stored
    public Task<bool> SaveRolesAsync(Account account, CancellationToken cancellationToken)
    {
        return Task.FromResult(_accounts.Values.Any(stored => stored.Id == account.Id));
    }
}
