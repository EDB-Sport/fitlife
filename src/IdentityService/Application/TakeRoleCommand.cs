using IdentityService.Domain;

namespace IdentityService.Application;

/// <summary>An Admin takes a role from an account (#365).</summary>
/// <param name="AccountId">The account that loses the role.</param>
/// <param name="Role">The role.</param>
/// <param name="AdminAccountId">The Admin's account id, from their token, for the log.</param>
public sealed record TakeRoleCommand(Guid AccountId, Role Role, string AdminAccountId);
