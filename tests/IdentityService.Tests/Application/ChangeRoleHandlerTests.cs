using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace IdentityService.Tests.Application;

/// <summary>An Admin gives or takes the role Træner (#365): the account decides, the handler saves only a change.</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // fresh mocks for every test, so no call counts carry over
public sealed class ChangeRoleHandlerTests
{
    private const string Hash = "AQAAAAIAAYagAAAAEhash";
    private const string AdminAccountId = "the-admins-account-id";

    private readonly Mock<IAccountRepository> _repository = new();

    [SetUp]
    public void EverySaveFindsTheAccount()
    {
        _repository.Setup(accounts => accounts.SaveRolesAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    [Test]
    public async Task Handle_GiveRoleToAnUnknownAccount_SaysNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        ChangeRoleOutcome outcome = await Handler().HandleAsync(new GiveRoleCommand(id, Role.Traener, AdminAccountId), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(ChangeRoleOutcome.AccountNotFound));
    }

    [Test]
    public async Task Handle_GiveRole_SavesTheNewRoles()
    {
        // Arrange
        Account account = Stored(Account.Create("anna@fitlife.example", Hash));

        // Act
        ChangeRoleOutcome outcome = await Handler().HandleAsync(new GiveRoleCommand(account.Id, Role.Traener, AdminAccountId), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(ChangeRoleOutcome.Changed));
        _repository.Verify(accounts => accounts.SaveRolesAsync(It.Is<Account>(saved => saved.Id == account.Id && saved.Roles.Contains(Role.Traener)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_GiveARoleItHas_DoesNotSave()
    {
        // Arrange
        Account account = Stored(Account.Create("bo@fitlife.example", Hash, Role.Traener));

        // Act
        ChangeRoleOutcome outcome = await Handler().HandleAsync(new GiveRoleCommand(account.Id, Role.Traener, AdminAccountId), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(ChangeRoleOutcome.NothingToChange));
        _repository.Verify(accounts => accounts.SaveRolesAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TakeRole_SavesTheRoles()
    {
        // Arrange
        var account = Account.Create("anna@fitlife.example", Hash);
        account.GiveRole(Role.Traener);
        Stored(account);

        // Act
        ChangeRoleOutcome outcome = await Handler().HandleAsync(new TakeRoleCommand(account.Id, Role.Traener, AdminAccountId), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(ChangeRoleOutcome.Changed));
        _repository.Verify(accounts => accounts.SaveRolesAsync(It.Is<Account>(saved => !saved.Roles.Contains(Role.Traener)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_TakeTheLastRole_SaysSoAndDoesNotSave()
    {
        // Arrange
        Account account = Stored(Account.Create("bo@fitlife.example", Hash, Role.Traener));

        // Act
        ChangeRoleOutcome outcome = await Handler().HandleAsync(new TakeRoleCommand(account.Id, Role.Traener, AdminAccountId), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(ChangeRoleOutcome.LastRoleKept));
        _repository.Verify(accounts => accounts.SaveRolesAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TakeRoleFromAnUnknownAccount_SaysNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        ChangeRoleOutcome outcome = await Handler().HandleAsync(new TakeRoleCommand(id, Role.Traener, AdminAccountId), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(ChangeRoleOutcome.AccountNotFound));
    }

    [Test]
    public async Task Handle_WhenTheAccountIsGoneBeforeTheSave_SaysNotFound()
    {
        // Arrange
        Account account = Stored(Account.Create("anna@fitlife.example", Hash));
        _repository.Setup(accounts => accounts.SaveRolesAsync(account, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        ChangeRoleOutcome outcome = await Handler().HandleAsync(new GiveRoleCommand(account.Id, Role.Traener, AdminAccountId), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome, Is.EqualTo(ChangeRoleOutcome.AccountNotFound));
    }

    private Account Stored(Account account)
    {
        _repository.Setup(accounts => accounts.FindByIdAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        return account;
    }

    private ChangeRoleHandler Handler()
    {
        return new ChangeRoleHandler(_repository.Object, NullLogger<ChangeRoleHandler>.Instance);
    }
}
