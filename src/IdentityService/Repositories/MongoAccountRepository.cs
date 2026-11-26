using IdentityService.Application;
using IdentityService.Domain;
using MongoDB.Driver;

namespace IdentityService.Repositories;

/// <summary>
/// Stores accounts in the <c>accounts</c> collection. A unique index that ignores letter case guards the e-mail address. The index
/// is created on first use, so nothing connects when the repository is registered.
/// </summary>
public sealed class MongoAccountRepository : IAccountRepository, IDisposable
{
    /// <summary>Compares e-mail addresses without letter case (strength 2: case-insensitive, diacritics still count).</summary>
    private static readonly Collation IgnoreLetterCase = new("en", strength: CollationStrength.Secondary);

    private readonly IMongoCollection<AccountDocument> _accounts;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexCreated;

    /// <summary>Uses the <c>accounts</c> collection of the database.</summary>
    /// <param name="database">The service's own database.</param>
    public MongoAccountRepository(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _accounts = database.GetCollection<AccountDocument>("accounts");
    }

    /// <inheritdoc />
    public async Task<bool> AddAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);
        var document = new AccountDocument
        {
            Id = account.Id,
            Email = account.Email,
            PasswordHash = account.PasswordHash,
            Roles = [.. account.Roles.Select(role => role.Name)],
        };
        try
        {
            await _accounts.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);
        AccountDocument? document = await _accounts
            .Find(stored => stored.Email == email, new FindOptions { Collation = IgnoreLetterCase })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        return ToAccount(document);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken)
    {
        List<AccountDocument> documents = await _accounts
            .Find(FilterDefinition<AccountDocument>.Empty)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. documents.Select(ToAccount)];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _indexLock.Dispose();
    }

    private static Account ToAccount(AccountDocument document)
    {
        return new Account(document.Id, document.Email, document.PasswordHash, document.Roles.Select(Role.FromName));
    }

    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        if (_indexCreated)
        {
            return;
        }

        await _indexLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_indexCreated)
            {
                var uniqueEmail = new CreateIndexModel<AccountDocument>(
                    Builders<AccountDocument>.IndexKeys.Ascending(stored => stored.Email),
                    new CreateIndexOptions { Name = "email_unique", Unique = true, Collation = IgnoreLetterCase });
                await _accounts.Indexes.CreateOneAsync(uniqueEmail, options: null, cancellationToken).ConfigureAwait(false);
                _indexCreated = true;
            }
        }
        finally
        {
            _indexLock.Release();
        }
    }
}
