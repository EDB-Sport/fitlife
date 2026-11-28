using McpServer.Guardrails;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace McpServer.Tests.Guardrails;

/// <summary>
/// The account a call to <c>/mcp</c> comes from: the <c>sub</c> claim of the caller's token (#216). Read without checking the signature,
/// only to pick the rate limit and to write the call log; Booking checks every forwarded token.
/// </summary>
public sealed class CallerAccountTests
{
    [Test]
    public void AccountIdFrom_ATokenWithASub_ReturnsTheAccountId()
    {
        // Arrange
        string header = "Bearer " + TokenWith(new Dictionary<string, object>(StringComparer.Ordinal) { ["sub"] = "2f0c5d4e-0000-4000-8000-000000000001" });

        // Act
        string? accountId = CallerAccount.AccountIdFrom(header);

        // Assert
        Assert.That(accountId, Is.EqualTo("2f0c5d4e-0000-4000-8000-000000000001"));
    }

    [Test]
    public void AccountIdFrom_ALowerCaseBearer_ReturnsTheAccountId()
    {
        // Arrange: the scheme is case-insensitive (RFC 9110 section 11.1)
        string header = "bearer " + TokenWith(new Dictionary<string, object>(StringComparer.Ordinal) { ["sub"] = "account-1" });

        // Act
        string? accountId = CallerAccount.AccountIdFrom(header);

        // Assert
        Assert.That(accountId, Is.EqualTo("account-1"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Bearer")]
    [TestCase("Bearer ")]
    [TestCase("Bearer not-a-token")]
    [TestCase("Bearer a.b.c")]
    [TestCase("Basic dXNlcjpwYXNz")]
    public void AccountIdFrom_SomethingThatIsNotAToken_ReturnsNothing(string? header)
    {
        // Act
        string? accountId = CallerAccount.AccountIdFrom(header);

        // Assert
        Assert.That(accountId, Is.Null);
    }

    [Test]
    public void AccountIdFrom_ATokenWithoutTheBearerScheme_ReturnsNothing()
    {
        // Arrange
        string header = TokenWith(new Dictionary<string, object>(StringComparer.Ordinal) { ["sub"] = "account-1" });

        // Act
        string? accountId = CallerAccount.AccountIdFrom(header);

        // Assert
        Assert.That(accountId, Is.Null);
    }

    [Test]
    public void AccountIdFrom_ATokenWithoutASub_ReturnsNothing()
    {
        // Arrange
        string header = "Bearer " + TokenWith(new Dictionary<string, object>(StringComparer.Ordinal) { ["role"] = "Medlem" });

        // Act
        string? accountId = CallerAccount.AccountIdFrom(header);

        // Assert
        Assert.That(accountId, Is.Null);
    }

    [Test]
    public void AccountIdFrom_ATokenWithAnEmptySub_ReturnsNothing()
    {
        // Arrange
        string header = "Bearer " + TokenWith(new Dictionary<string, object>(StringComparer.Ordinal) { ["sub"] = string.Empty });

        // Act
        string? accountId = CallerAccount.AccountIdFrom(header);

        // Assert
        Assert.That(accountId, Is.Null);
    }

    private static string TokenWith(Dictionary<string, object> claims)
    {
        // Unsigned is enough: the signature isn't checked here
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Claims = claims });
    }
}
