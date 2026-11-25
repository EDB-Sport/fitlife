namespace IdentityService.Application;

/// <summary>What happened to a <see cref="CreateAccountCommand"/>.</summary>
public enum CreateAccountResult
{
    /// <summary>The account is stored, with the role Medlem.</summary>
    Created,

    /// <summary>Another account has the e-mail address, in some letter case. Nothing is stored.</summary>
    EmailAlreadyTaken,
}
