namespace MemberPortal.Web.Clients;

/// <summary>A person logs in: Identity's <c>LogInRequest</c> (contracts/openapi/identityservice.json).</summary>
/// <param name="Email">The e-mail address of the account, in any letter case.</param>
/// <param name="Password">The password of the account.</param>
public sealed record LogInRequest(string Email, string Password);
