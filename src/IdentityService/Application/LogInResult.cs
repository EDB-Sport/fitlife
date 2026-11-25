namespace IdentityService.Application;

/// <summary>The token a <see cref="LogInCommand"/> gives.</summary>
/// <param name="AccessToken">The signed JWT with the account's id and roles.</param>
/// <param name="ExpiresAt">When the token stops being accepted.</param>
public sealed record LogInResult(string AccessToken, DateTimeOffset ExpiresAt);
