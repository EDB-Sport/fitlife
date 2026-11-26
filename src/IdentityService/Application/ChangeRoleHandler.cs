using IdentityService.Domain;

namespace IdentityService.Application;

/// <summary>
/// Handles <see cref="GiveRoleCommand"/> and <see cref="TakeRoleCommand"/> (#365): loads the account, lets it decide, and stores
/// the roles only when they changed. The token carries the roles from log-in, so a change applies from the next log-in (ADR 009).
/// </summary>
public sealed partial class ChangeRoleHandler
{
    private readonly IAccountRepository _repository;
    private readonly ILogger<ChangeRoleHandler> _logger;

    /// <summary>Creates the handler.</summary>
    /// <param name="repository">Where accounts are stored.</param>
    /// <param name="logger">Logs each change.</param>
    public ChangeRoleHandler(IAccountRepository repository, ILogger<ChangeRoleHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>Gives the account the role.</summary>
    /// <param name="command">The account, the role and the Admin.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>What changed.</returns>
    public Task<ChangeRoleOutcome> HandleAsync(GiveRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ChangeAsync(command.AccountId, command.AdminAccountId, $"gave the role {command.Role.Name}", account => account.GiveRole(command.Role), cancellationToken);
    }

    /// <summary>Takes the role from the account.</summary>
    /// <param name="command">The account, the role and the Admin.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>What changed.</returns>
    public Task<ChangeRoleOutcome> HandleAsync(TakeRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ChangeAsync(command.AccountId, command.AdminAccountId, $"took the role {command.Role.Name}", account => account.TakeRole(command.Role), cancellationToken);
    }

    private async Task<ChangeRoleOutcome> ChangeAsync(Guid accountId, string adminAccountId, string change, Func<Account, RoleChange> decide, CancellationToken cancellationToken)
    {
        Account? account = await _repository.FindByIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return ChangeRoleOutcome.AccountNotFound;
        }

        switch (decide(account))
        {
            case RoleChange.NothingToChange:
                return ChangeRoleOutcome.NothingToChange;
            case RoleChange.LastRoleKept:
                return ChangeRoleOutcome.LastRoleKept;
            default:
                break;
        }

        if (!await _repository.SaveRolesAsync(account, cancellationToken).ConfigureAwait(false))
        {
            return ChangeRoleOutcome.AccountNotFound;
        }

        LogRoleChanged(adminAccountId, change, accountId);
        return ChangeRoleOutcome.Changed;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {AdminAccountId} {Change} for account {AccountId}")]
    private partial void LogRoleChanged(string adminAccountId, string change, Guid accountId);
}
