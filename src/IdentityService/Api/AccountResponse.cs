using IdentityService.Domain;

namespace IdentityService.Api;

/// <summary>An account, as POST /accounts and GET /accounts answer. Never the password or its hash.</summary>
/// <param name="Id">The account's id.</param>
/// <param name="Email">The e-mail address to log in with.</param>
/// <param name="Roles">The glossary's names of the account's roles.</param>
public sealed record AccountResponse(Guid Id, string Email, IReadOnlyList<string> Roles)
{
    /// <summary>The response for an account: its id, e-mail address and the names of its roles, and nothing else.</summary>
    /// <param name="account">The account.</param>
    /// <returns>The response.</returns>
    public static AccountResponse From(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new AccountResponse(account.Id, account.Email, [.. account.Roles.Select(role => role.Name)]);
    }
}
