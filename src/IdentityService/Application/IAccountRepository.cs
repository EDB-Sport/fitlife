using IdentityService.Domain;

namespace IdentityService.Application;

/// <summary>Stores accounts. E-mail addresses that differ only in letter case are the same e-mail address.</summary>
public interface IAccountRepository
{
    /// <summary>Adds the account, unless another account already has its e-mail address.</summary>
    /// <param name="account">The new account.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when the e-mail address was free and the account is stored; otherwise nothing changes.</returns>
    Task<bool> AddAsync(Account account, CancellationToken cancellationToken);

    /// <summary>Finds the account with the e-mail address, in any letter case.</summary>
    /// <param name="email">The e-mail address.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The account, or <see langword="null"/> when no account has that e-mail address.</returns>
    Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}
