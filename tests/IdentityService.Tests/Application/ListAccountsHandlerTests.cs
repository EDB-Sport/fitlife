using IdentityService.Application;
using IdentityService.Domain;
using Moq;

namespace IdentityService.Tests.Application;

/// <summary>An Admin lists the accounts (#364): every account, with its roles, ordered by e-mail address.</summary>
public sealed class ListAccountsHandlerTests
{
    private const string Hash = "AQAAAAIAAYagAAAAEhash";

    private static readonly string[] EmailsInOrder = ["Anna@fitlife.example", "bo@fitlife.example"];

    [Test]
    public async Task Handle_ReturnsEveryAccountWithItsRoles()
    {
        // Arrange
        var anna = Account.Create("anna@fitlife.example", Hash);
        var bo = Account.Create("bo@fitlife.example", Hash, Role.Traener);
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<Account>)[anna, bo]);
        var handler = new ListAccountsHandler(repository.Object);

        // Act
        IReadOnlyList<Account> accounts = await handler.HandleAsync(new ListAccountsQuery(), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(accounts.Select(account => account.Id), Is.EqualTo(new[] { anna.Id, bo.Id }));
        Assert.That(accounts.Select(account => account.Roles), Is.EqualTo(new[] { new[] { Role.Medlem }, new[] { Role.Traener } }));
    }

    [Test]
    public async Task Handle_OrdersTheAccountsByEmail()
    {
        // Arrange
        var bo = Account.Create("bo@fitlife.example", Hash);
        var anna = Account.Create("Anna@fitlife.example", Hash);
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<Account>)[bo, anna]);
        var handler = new ListAccountsHandler(repository.Object);

        // Act
        IReadOnlyList<Account> accounts = await handler.HandleAsync(new ListAccountsQuery(), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(accounts.Select(account => account.Email), Is.EqualTo(EmailsInOrder));
    }

    [Test]
    public void Handle_WithoutAQuery_Throws()
    {
        // Arrange
        var handler = new ListAccountsHandler(new Mock<IAccountRepository>().Object);

        // Act & Assert
        // ! The test passes null on purpose, to check the guard.
        Assert.That(() => handler.HandleAsync(null!, CancellationToken.None), Throws.ArgumentNullException);
    }
}
