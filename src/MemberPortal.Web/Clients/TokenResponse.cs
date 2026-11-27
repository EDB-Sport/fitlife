namespace MemberPortal.Web.Clients;

/// <summary>The token for a logged-in account: Identity's <c>TokenResponse</c> (contracts/openapi/identityservice.json).</summary>
/// <param name="AccessToken">The JWT the portal sends as <c>Authorization: Bearer</c> to the other services.</param>
/// <param name="ExpiresAt">When the token stops being accepted.</param>
public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
