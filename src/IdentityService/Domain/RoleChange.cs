namespace IdentityService.Domain;

/// <summary>What <see cref="Account.GiveRole"/> or <see cref="Account.TakeRole"/> did (#365).</summary>
public enum RoleChange
{
    /// <summary>The account's roles changed.</summary>
    Changed,

    /// <summary>The account already had the role, or never had it: nothing changed.</summary>
    NothingToChange,

    /// <summary>The role is the account's only one, so it stays: an account always has a role.</summary>
    LastRoleKept,
}
