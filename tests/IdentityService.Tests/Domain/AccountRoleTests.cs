using IdentityService.Domain;

namespace IdentityService.Tests.Domain;

/// <summary>An Admin gives an account a role or takes it back (#365). An account always keeps at least one role.</summary>
public sealed class AccountRoleTests
{
    private const string Email = "anna@fitlife.example";
    private const string PasswordHash = "AQAAAAIAAYagAAAAEhash";

    [Test]
    public void GiveRole_ANewRole_AddsIt()
    {
        // Arrange
        var account = Account.Create(Email, PasswordHash);

        // Act
        RoleChange change = account.GiveRole(Role.Traener);

        // Assert
        Assert.That((change, account.Roles), Is.EqualTo((RoleChange.Changed, new[] { Role.Medlem, Role.Traener })));
    }

    [Test]
    public void GiveRole_ARoleItHas_ChangesNothing()
    {
        // Arrange
        var account = Account.Create(Email, PasswordHash, Role.Traener);

        // Act
        RoleChange change = account.GiveRole(Role.Traener);

        // Assert
        Assert.That((change, account.Roles), Is.EqualTo((RoleChange.NothingToChange, new[] { Role.Traener })));
    }

    [Test]
    public void TakeRole_ARoleItHas_RemovesIt()
    {
        // Arrange
        var account = Account.Create(Email, PasswordHash);
        account.GiveRole(Role.Traener);

        // Act
        RoleChange change = account.TakeRole(Role.Traener);

        // Assert
        Assert.That((change, account.Roles), Is.EqualTo((RoleChange.Changed, new[] { Role.Medlem })));
    }

    [Test]
    public void TakeRole_ARoleItHasNot_ChangesNothing()
    {
        // Arrange
        var account = Account.Create(Email, PasswordHash);

        // Act
        RoleChange change = account.TakeRole(Role.Traener);

        // Assert
        Assert.That((change, account.Roles), Is.EqualTo((RoleChange.NothingToChange, new[] { Role.Medlem })));
    }

    [Test]
    public void TakeRole_TheLastRole_IsRejected()
    {
        // Arrange
        var account = Account.Create(Email, PasswordHash, Role.Traener);

        // Act
        RoleChange change = account.TakeRole(Role.Traener);

        // Assert
        Assert.That((change, account.Roles), Is.EqualTo((RoleChange.LastRoleKept, new[] { Role.Traener })));
    }

    [Test]
    public void GiveRole_WithoutARole_Throws()
    {
        // Arrange
        var account = Account.Create(Email, PasswordHash);

        // Act & Assert
        // ! The test passes null on purpose, to check the guard.
        Assert.That(() => account.GiveRole(null!), Throws.ArgumentNullException);
    }
}
