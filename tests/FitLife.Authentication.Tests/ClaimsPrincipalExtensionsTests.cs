using System.Security.Claims;

namespace FitLife.Authentication.Tests;

/// <summary>Every service reads the account id from the token the same way (#362).</summary>
public sealed class ClaimsPrincipalExtensionsTests
{
    [Test]
    public void GetAccountId_WithTheClaim_ReturnsTheSubClaim()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(FitLifeJwt.AccountIdClaim, "account-1")], "Bearer"));

        // Act
        string accountId = user.GetAccountId();

        // Assert
        Assert.That(accountId, Is.EqualTo("account-1"));
    }

    [Test]
    public void GetAccountId_WithoutTheClaim_Throws()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(FitLifeJwt.RoleClaim, FitLifeRoles.Medlem)], "Bearer"));

        // Act
        Func<string> act = () => user.GetAccountId();

        // Assert
        Assert.That(act, Throws.InvalidOperationException.With.Message.Contains(FitLifeJwt.AccountIdClaim));
    }
}
