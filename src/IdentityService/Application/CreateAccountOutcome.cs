using IdentityService.Domain;

namespace IdentityService.Application;

/// <summary>The answer to a <see cref="CreateAccountCommand"/>.</summary>
/// <param name="Result">What happened.</param>
/// <param name="Account">The new account when <paramref name="Result"/> is <see cref="CreateAccountResult.Created"/>; otherwise null.</param>
public sealed record CreateAccountOutcome(CreateAccountResult Result, Account? Account);
