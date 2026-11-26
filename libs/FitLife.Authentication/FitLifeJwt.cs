namespace FitLife.Authentication;

/// <summary>
/// The values Identity signs its tokens with (#169), in one place so no service drifts from them
/// (CONTEXT-MAP section 4: every service checks the same token in the same way).
/// </summary>
public static class FitLifeJwt
{
    /// <summary>The configuration key of the HMAC-SHA256 signing key. Vault seeds it from <c>JWT_SIGNING_KEY</c> (#17).</summary>
    public const string SigningKeyConfigurationKey = "Jwt:SigningKey";

    /// <summary>The issuer Identity writes in every token.</summary>
    public const string Issuer = "fitlife-identity";

    /// <summary>The audience of every token: the services of FitLife Digital.</summary>
    public const string Audience = "fitlife";

    /// <summary>The claim that carries the id of the <c>Account</c>.</summary>
    public const string AccountIdClaim = "sub";

    /// <summary>The claim that carries a <c>Role</c> (Medlem, Træner or Admin). One claim per role.</summary>
    public const string RoleClaim = "role";

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits.</summary>
    public const int MinimumSigningKeyBytes = 32;
}
