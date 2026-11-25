using IdentityService.Domain;
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Application;

/// <summary>Handles <see cref="LogInCommand"/>: checks the password against the stored hash and writes a token.</summary>
public sealed class LogInHandler
{
    // An account nobody can log in as, hashed once: an unknown e-mail address is checked against it
    private static readonly Lazy<Account> UnknownAccount = new(CreateUnknownAccount);

    private readonly IAccountRepository _repository;
    private readonly IPasswordHasher<Account> _hasher;
    private readonly TokenWriter _writer;

    /// <summary>Creates the handler.</summary>
    /// <param name="repository">Where accounts are stored.</param>
    /// <param name="hasher">Checks the password against the hash.</param>
    /// <param name="writer">Writes the token.</param>
    public LogInHandler(IAccountRepository repository, IPasswordHasher<Account> hasher, TokenWriter writer)
    {
        _repository = repository;
        _hasher = hasher;
        _writer = writer;
    }

    /// <summary>Logs in with an e-mail address and a password.</summary>
    /// <param name="command">The e-mail address and the password.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The token, or <see langword="null"/> when no account has the e-mail address or the password is wrong. The caller can't tell which.</returns>
    public async Task<LogInResult?> HandleAsync(LogInCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Account? account = await _repository.FindByEmailAsync(command.Email, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            // Do the same hashing work as for a wrong password, so the response time doesn't tell whether the e-mail address has an account
            _ = _hasher.VerifyHashedPassword(UnknownAccount.Value, UnknownAccount.Value.PasswordHash, command.Password);
            return null;
        }

        if (_hasher.VerifyHashedPassword(account, account.PasswordHash, command.Password) == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return _writer.Write(account);
    }

    private static Account CreateUnknownAccount()
    {
        // ! PasswordHasher<TUser> doesn't read the user.
        string passwordHash = new PasswordHasher<Account>().HashPassword(null!, Guid.NewGuid().ToString());
        return Account.Create("unknown@fitlife.invalid", passwordHash);
    }
}
