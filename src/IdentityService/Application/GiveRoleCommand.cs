using IdentityService.Domain;

namespace IdentityService.Application;

/// <summary>An Admin gives an account a role (#365).</summary>
/// <param name="AccountId">The account that gets the role.</param>
/// <param name="Role">The role.</param>
/// <param name="AdminAccountId">The Admin's account id, from their token, for the log.</param>
public sealed record GiveRoleCommand(Guid AccountId, Role Role, string AdminAccountId);
