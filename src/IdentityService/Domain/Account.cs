namespace IdentityService.Domain;

/// <summary>
/// Konto: the log-in identity of a person (GLOSSARY.md). The aggregate root of Identity: its e-mail address is the log-in name,
/// and it holds only the hash of the password, never the password.
/// </summary>
public sealed class Account
{
    private readonly List<Role> _roles;

    internal Account(Guid id, string email, string passwordHash, IEnumerable<Role> roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        _roles = [.. roles];
    }

    /// <summary>The account's id. Other contexts keep this id, never the account.</summary>
    public Guid Id { get; }

    /// <summary>The e-mail address the person logs in with. Letter case doesn't matter when it is looked up.</summary>
    public string Email { get; }

    /// <summary>The hash of the password.</summary>
    public string PasswordHash { get; }

    /// <summary>What the account may do.</summary>
    public IReadOnlyList<Role> Roles => _roles.AsReadOnly();

    /// <summary>Gives the account a role it doesn't have yet (#365). Giving a role it has changes nothing.</summary>
    /// <param name="role">The role to give.</param>
    /// <returns>Whether the roles changed.</returns>
    public RoleChange GiveRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (_roles.Contains(role))
        {
            return RoleChange.NothingToChange;
        }

        _roles.Add(role);
        return RoleChange.Changed;
    }

    /// <summary>Takes a role from the account (#365). Taking a role it hasn't changes nothing; its last role stays.</summary>
    /// <param name="role">The role to take.</param>
    /// <returns>Whether the roles changed, or that the role is the last one.</returns>
    public RoleChange TakeRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (!_roles.Contains(role))
        {
            return RoleChange.NothingToChange;
        }

        if (_roles.Count == 1)
        {
            return RoleChange.LastRoleKept;
        }

        _roles.Remove(role);
        return RoleChange.Changed;
    }

    /// <summary>Creates a new account with the role <see cref="Role.Medlem"/>.</summary>
    /// <param name="email">The e-mail address the person logs in with.</param>
    /// <param name="passwordHash">The hash of the password. The password itself never reaches the model.</param>
    /// <returns>The new account, with a new id.</returns>
    /// <exception cref="ArgumentException">The e-mail address or the hash is missing.</exception>
    public static Account Create(string email, string passwordHash)
    {
        return Create(email, passwordHash, Role.Medlem);
    }

    /// <summary>Creates a new account with the one role <paramref name="role"/>, e.g. a demo account for Træner or Admin.</summary>
    /// <param name="email">The e-mail address the person logs in with.</param>
    /// <param name="passwordHash">The hash of the password. The password itself never reaches the model.</param>
    /// <param name="role">The account's role.</param>
    /// <returns>The new account, with a new id.</returns>
    /// <exception cref="ArgumentException">The e-mail address or the hash is missing.</exception>
    /// <exception cref="ArgumentNullException">The role is missing.</exception>
    public static Account Create(string email, string passwordHash, Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return new Account(Guid.NewGuid(), email, passwordHash, [role]);
    }
}
