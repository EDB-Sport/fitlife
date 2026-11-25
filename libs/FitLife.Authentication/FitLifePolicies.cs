namespace FitLife.Authentication;

/// <summary>
/// The names of the policies <see cref="AuthorizationExtensions.AddFitLifeAuthorization"/> registers.
/// Pick one with <c>RequireAuthorization(FitLifePolicies.Admin)</c>. An endpoint without one only needs a valid token,
/// and one marked <c>AllowAnonymous()</c> (<c>/health</c>, the OpenAPI document and Scalar in Development) needs none.
/// </summary>
public static class FitLifePolicies
{
    /// <summary>The role <see cref="FitLifeRoles.Medlem"/>.</summary>
    public const string Medlem = "Medlem";

    /// <summary>The role <see cref="FitLifeRoles.Traener"/> or <see cref="FitLifeRoles.Admin"/>.</summary>
    public const string TraenerOrAdmin = "TraenerOrAdmin";

    /// <summary>The role <see cref="FitLifeRoles.Admin"/> only.</summary>
    public const string Admin = "Admin";
}
