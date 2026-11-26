namespace IdentityService.Api;

/// <summary>The token for a logged-in account.</summary>
/// <param name="AccessToken">The JWT to send as <c>Authorization: Bearer</c> to the other services.</param>
/// <param name="ExpiresAt">When the token stops being accepted: 60 minutes after it was written.</param>
public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
