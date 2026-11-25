using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace IdentityService.Tests.Application;

/// <summary>Creating an account (#234): the first of the two sign-up steps. Only a hash of the password is stored.</summary>
public sealed class CreateAccountHandlerTests
{
    private const string Email = "anna@fitlife.example";
    private const string Password = "made-up-password-1";

    private readonly PasswordHasher<Account> _hasher = new();

    [Test]
    public async Task Handle_WhenTheEmailIsTaken_IsRejected()
    {
        // Arrange
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account.Create(Email, "AQAAAAIAAYagAAAAEhash"));
        var handler = new CreateAccountHandler(repository.Object, _hasher);

        // Act
        CreateAccountOutcome outcome = await handler.HandleAsync(new CreateAccountCommand(Email, Password), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome.Result, Is.EqualTo(CreateAccountResult.EmailAlreadyTaken));
        Assert.That(outcome.Account, Is.Null);
        repository.Verify(accounts => accounts.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_WhenTheEmailIsTakenMeanwhile_IsRejected()
    {
        // Arrange
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        repository.Setup(accounts => accounts.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var handler = new CreateAccountHandler(repository.Object, _hasher);

        // Act
        CreateAccountOutcome outcome = await handler.HandleAsync(new CreateAccountCommand(Email, Password), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(outcome.Result, Is.EqualTo(CreateAccountResult.EmailAlreadyTaken));
        Assert.That(outcome.Account, Is.Null);
    }

    [Test]
    public async Task Handle_WithANewEmail_StoresAPasswordHashAndNotThePassword()
    {
        // Arrange
        Account? stored = null;
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        repository.Setup(accounts => accounts.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((account, _) => stored = account)
            .ReturnsAsync(true);
        var handler = new CreateAccountHandler(repository.Object, _hasher);

        // Act
        CreateAccountOutcome outcome = await handler.HandleAsync(new CreateAccountCommand(Email, Password), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Account account = stored ?? throw new AssertionException("No account was stored.");
        Assert.That(outcome.Result, Is.EqualTo(CreateAccountResult.Created));
        Assert.That(outcome.Account, Is.SameAs(account));
        Assert.That(account.Email, Is.EqualTo(Email));
        Assert.That(account.PasswordHash, Does.Not.Contain(Password));
        Assert.That(_hasher.VerifyHashedPassword(account, account.PasswordHash, Password), Is.EqualTo(PasswordVerificationResult.Success));
    }
}
