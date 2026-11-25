namespace IdentityService.Domain;

/// <summary>Rolle: what an account may do (GLOSSARY.md). Exactly <see cref="Medlem"/>, <see cref="Traener"/> and <see cref="Admin"/>.</summary>
public sealed record Role
{
    private Role(string name)
    {
        Name = name;
    }

    /// <summary>A member of the fitness center. Every new account has this role.</summary>
    public static Role Medlem { get; } = new("Medlem");

    /// <summary>A trainer who leads class sessions.</summary>
    public static Role Traener { get; } = new("Træner");

    /// <summary>Runs the fitness center's system.</summary>
    public static Role Admin { get; } = new("Admin");

    /// <summary>Every role, in the glossary's order.</summary>
    public static IReadOnlyList<Role> All { get; } = [Medlem, Traener, Admin];

    /// <summary>The glossary's name of the role, also the value of the role claim in the token.</summary>
    public string Name { get; }

    /// <summary>The role with the glossary's name <paramref name="name"/>, in that exact spelling.</summary>
    /// <param name="name">"Medlem", "Træner" or "Admin".</param>
    /// <returns>The role.</returns>
    /// <exception cref="ArgumentException">No role has that name.</exception>
    public static Role FromName(string name)
    {
        return All.FirstOrDefault(role => string.Equals(role.Name, name, StringComparison.Ordinal))
            ?? throw new ArgumentException($"No role is named '{name}'.", nameof(name));
    }
}
