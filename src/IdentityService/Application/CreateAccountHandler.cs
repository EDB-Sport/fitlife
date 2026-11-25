using IdentityService.Domain;
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Application;

/// <summary>Handles <see cref="CreateAccountCommand"/>: hashes the password and stores a new account, unless the e-mail address is taken.</summary>
public sealed class CreateAccountHandler
{
    private readonly IAccountRepository _repository;
    private readonly IPasswordHasher<Account> _hasher;
    private readonly ILogger<CreateAccountHandler> _logger;

    /// <summary>Creates the handler.</summary>
    /// <param name="repository">Where accounts are stored.</param>
    /// <param name="hasher">Hashes the password.</param>
    /// <param name="logger">Logs the new account's id, never the password or the hash.</param>
    public CreateAccountHandler(IAccountRepository repository, IPasswordHasher<Account> hasher, ILogger<CreateAccountHandler> logger)
    {
        _repository = repository;
        _hasher = hasher;
        _logger = logger;
    }

    /// <summary>Stores a new account with the role Medlem.</summary>
    /// <param name="command">The e-mail address and the password.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see cref="CreateAccountResult.Created"/> with the account, or <see cref="CreateAccountResult.EmailAlreadyTaken"/>.</returns>
    public async Task<CreateAccountOutcome> HandleAsync(CreateAccountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Stryker disable once Boolean : ConfigureAwait(false) -> true is an equivalent mutant (#270)
        if (await _repository.FindByEmailAsync(command.Email, cancellationToken).ConfigureAwait(false) is not null)
        {
            return new CreateAccountOutcome(CreateAccountResult.EmailAlreadyTaken, null);
        }

        // ! PasswordHasher<TUser> doesn't read the user, and the account can't exist before its hash does.
        string passwordHash = _hasher.HashPassword(null!, command.Password);
        var account = Account.Create(command.Email, passwordHash);
        // Stryker disable once Boolean : ConfigureAwait(false) -> true is an equivalent mutant (#270)
        if (!await _repository.AddAsync(account, cancellationToken).ConfigureAwait(false))
        {
            return new CreateAccountOutcome(CreateAccountResult.EmailAlreadyTaken, null);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Account {AccountId} created", account.Id);
        }

        return new CreateAccountOutcome(CreateAccountResult.Created, account);
    }
}
