using IdentityService.Domain;

namespace IdentityService.Application;

/// <summary>Handles <see cref="ListAccountsQuery"/>: every account, ordered by e-mail address.</summary>
public sealed class ListAccountsHandler
{
    private readonly IAccountRepository _repository;

    /// <summary>Creates the handler.</summary>
    /// <param name="repository">Where accounts are stored.</param>
    public ListAccountsHandler(IAccountRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Reads every account.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accounts, ordered by e-mail address without regard to letter case.</returns>
    public async Task<IReadOnlyList<Account>> HandleAsync(ListAccountsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IReadOnlyList<Account> accounts = await _repository.ListAsync(cancellationToken).ConfigureAwait(false);
        return [.. accounts.OrderBy(account => account.Email, StringComparer.OrdinalIgnoreCase)];
    }
}
