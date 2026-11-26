using IdentityService.Domain;

namespace IdentityService.Tests.Domain;

/// <summary>Konto: the log-in identity of a person (GLOSSARY.md). The model holds only the password hash, never the password.</summary>
public sealed class AccountTests
{
    private const string Email = "anna@fitlife.example";
    private const string PasswordHash = "AQAAAAIAAYagAAAAEhash";

    [Test]
    public void Create_WithAnEmailAndAPasswordHash_HasRoleMedlem()
    {
        // Arrange & Act
        var account = Account.Create(Email, PasswordHash);

        // Assert
        Assert.That(account.Roles, Is.EqualTo(new[] { Role.Medlem }));
    }

    [Test]
    public void Create_WithARole_HasOnlyThatRole()
    {
        // Arrange & Act
        var account = Account.Create(Email, PasswordHash, Role.Traener);

        // Assert
        Assert.That(account.Roles, Is.EqualTo(new[] { Role.Traener }));
    }

    [Test]
    public void Create_WithoutARole_Throws()
    {
        // Arrange & Act & Assert
        // ! The test passes null on purpose, to check the guard.
        Assert.That(() => Account.Create(Email, PasswordHash, null!), Throws.ArgumentNullException);
    }

    [Test]
    public void Create_WithAnEmailAndAPasswordHash_KeepsBoth()
    {
        // Arrange & Act
        var account = Account.Create(Email, PasswordHash);

        // Assert
        Assert.That(account.Email, Is.EqualTo(Email));
        Assert.That(account.PasswordHash, Is.EqualTo(PasswordHash));
    }

    [Test]
    public void Create_TwoAccounts_HaveDifferentIds()
    {
        // Arrange
        var first = Account.Create(Email, PasswordHash);

        // Act
        var second = Account.Create("bo@fitlife.example", PasswordHash);

        // Assert
        Assert.That(second.Id, Is.Not.EqualTo(first.Id));
        Assert.That(second.Id, Is.Not.EqualTo(Guid.Empty));
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Create_WithoutAPasswordHash_Throws(string passwordHash)
    {
        // Arrange & Act & Assert
        Assert.That(() => Account.Create(Email, passwordHash), Throws.InstanceOf<ArgumentException>());
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Create_WithoutAnEmail_Throws(string email)
    {
        // Arrange & Act & Assert
        Assert.That(() => Account.Create(email, PasswordHash), Throws.InstanceOf<ArgumentException>());
    }
}
