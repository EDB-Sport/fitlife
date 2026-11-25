namespace FitLife.Authentication;

/// <summary>
/// The values of the role claim (<see cref="FitLifeJwt.RoleClaim"/>), one per <c>Role</c> (GLOSSARY, Rolle).
/// Check a role through a policy in <see cref="FitLifePolicies"/>, not by comparing these yourself.
/// </summary>
public static class FitLifeRoles
{
    /// <summary>The role Medlem: a member who books classes.</summary>
    public const string Medlem = "Medlem";

    /// <summary>The role Træner. The value keeps its æ; never "Trainer" (GLOSSARY, Avoid).</summary>
    public const string Traener = "Træner";

    /// <summary>The role Admin.</summary>
    public const string Admin = "Admin";
}
