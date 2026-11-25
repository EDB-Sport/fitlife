using MembershipService.Domain;

namespace MembershipService.Tests;

/// <summary>A member keeps the id of the account that joined (CONTEXT-MAP section 3, "Sign-up takes two steps").</summary>
public sealed class MemberTests
{
    [Test]
    public void Join_WithAnAccountId_KeepsTheAccountId()
    {
        // Arrange
        const string accountId = "account-1";

        // Act
        var member = Member.Join(accountId);

        // Assert
        Assert.That(member.AccountId, Is.EqualTo(accountId));
        Assert.That(member.Id, Is.Not.EqualTo(Guid.Empty));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Join_WithoutAnAccountId_IsRejected(string accountId)
    {
        // Arrange
        Action act = () => Member.Join(accountId);

        // Act & Assert
        Assert.That(act, Throws.ArgumentException);
    }
}
