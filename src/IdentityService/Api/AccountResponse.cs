namespace IdentityService.Api;

/// <summary>A new account. Never the password or its hash.</summary>
/// <param name="Id">The account's id.</param>
/// <param name="Email">The e-mail address to log in with.</param>
/// <param name="Roles">The glossary's names of the account's roles.</param>
public sealed record AccountResponse(Guid Id, string Email, IReadOnlyList<string> Roles);
