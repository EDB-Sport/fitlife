namespace IdentityService.Application;

/// <summary>What <see cref="ChangeRoleHandler"/> did (#365).</summary>
public enum ChangeRoleOutcome
{
    /// <summary>The account's roles changed and are stored.</summary>
    Changed,

    /// <summary>The account already had the role, or never had it: nothing to store.</summary>
    NothingToChange,

    /// <summary>No account has the id.</summary>
    AccountNotFound,

    /// <summary>The role is the account's only one, so it stays.</summary>
    LastRoleKept,
}
