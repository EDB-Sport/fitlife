namespace MemberPortal.Web.Clients;

/// <summary>A person asks for an account: Identity's <c>CreateAccountRequest</c> (contracts/openapi/identityservice.json).</summary>
/// <param name="Email">The e-mail address to log in with.</param>
/// <param name="Password">At least 8 characters. Identity checks it, not the portal.</param>
public sealed record CreateAccountRequest(string Email, string Password);
