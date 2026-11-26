namespace BookingService.IntegrationTests;

/// <summary>
/// Gives every BookingService the tests start the test signing key, in one place (#260): <c>AddFitLifeJwtAuthentication()</c>
/// checks <c>Jwt:SigningKey</c> at start-up, so no host starts without it. Compose gets the key from Vault.
/// </summary>
[SetUpFixture]
public sealed class JwtSigningKeySetUp
{
    private const string Variable = "Jwt__SigningKey";

    /// <summary>Sets the key for every host started in this test run.</summary>
    [OneTimeSetUp]
    public void SetTheTestKey()
    {
        Environment.SetEnvironmentVariable(Variable, TestTokens.SigningKey);
    }

    /// <summary>Removes it again.</summary>
    [OneTimeTearDown]
    public void RemoveTheTestKey()
    {
        Environment.SetEnvironmentVariable(Variable, null);
    }
}
