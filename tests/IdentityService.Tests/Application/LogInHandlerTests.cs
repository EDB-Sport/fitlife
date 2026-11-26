using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace IdentityService.Tests.Application;

/// <summary>Logging in (#235): the right e-mail address and password give a token. Anything else is rejected the same way.</summary>
public sealed class LogInHandlerTests
{
    private const string Email = "anna@fitlife.example";
    private const string Password = "made-up-password-1";
    private const string SigningKey = "test-signing-key-for-unit-tests-only-32+";

    private readonly PasswordHasher<Account> _hasher = new();

    private LogInHandler Handler(Mock<IAccountRepository> repository)
    {
        return Handler(repository, _hasher);
    }

    private static LogInHandler Handler(Mock<IAccountRepository> repository, IPasswordHasher<Account> hasher)
    {
        return Handler(repository, hasher, NullLogger<LogInHandler>.Instance);
    }

    internal static LogInHandler Handler(Mock<IAccountRepository> repository, IPasswordHasher<Account> hasher, ILogger<LogInHandler> logger)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Jwt:SigningKey"] = SigningKey })
            .Build();
        return new LogInHandler(repository.Object, hasher, new TokenWriter(configuration, new FakeTimeProvider()), logger);
    }

    [Test]
    public async Task Handle_WithAnUnknownEmail_StillVerifiesAPassword()
    {
        // Arrange: the response time mustn't tell whether an account has the e-mail address
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var hasher = new Mock<IPasswordHasher<Account>>();
        hasher.Setup(passwords => passwords.VerifyHashedPassword(It.IsAny<Account>(), It.IsAny<string>(), Password)).Returns(PasswordVerificationResult.Failed);

        // Act
        LogInResult? result = await Handler(repository, hasher.Object).HandleAsync(new LogInCommand(Email, Password), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result, Is.Null);
        hasher.Verify(passwords => passwords.VerifyHashedPassword(It.IsAny<Account>(), It.IsAny<string>(), Password), Times.Once);
    }

    [Test]
    public async Task Handle_WithAnUnknownEmail_IsRejectedEvenIfTheDummyHashMatches()
    {
        // Arrange
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
        var hasher = new Mock<IPasswordHasher<Account>>();
        hasher.Setup(passwords => passwords.VerifyHashedPassword(It.IsAny<Account>(), It.IsAny<string>(), It.IsAny<string>())).Returns(PasswordVerificationResult.Success);

        // Act
        LogInResult? result = await Handler(repository, hasher.Object).HandleAsync(new LogInCommand(Email, Password), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result, Is.Null);
    }

    private Account StoredAccount()
    {
        // ! PasswordHasher<TUser> doesn't read the user.
        return Account.Create(Email, _hasher.HashPassword(null!, Password));
    }

    [Test]
    public async Task Handle_WithAWrongPassword_IsRejected()
    {
        // Arrange
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(StoredAccount());

        // Act
        LogInResult? result = await Handler(repository).HandleAsync(new LogInCommand(Email, "the-wrong-password"), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Handle_WithAnUnknownEmail_IsRejected()
    {
        // Arrange
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

        // Act
        LogInResult? result = await Handler(repository).HandleAsync(new LogInCommand(Email, Password), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Handle_WithTheRightPassword_ReturnsAToken()
    {
        // Arrange
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(StoredAccount());

        // Act
        LogInResult? result = await Handler(repository).HandleAsync(new LogInCommand(Email, Password), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result?.AccessToken, Is.Not.Empty);
    }

    [Test]
    public void Handle_WithoutACommand_Throws()
    {
        // Arrange
        LogInHandler handler = Handler(new Mock<IAccountRepository>());

        // Act & Assert
        // ! The test passes null on purpose, to check the guard.
        Assert.That(() => handler.HandleAsync(null!, CancellationToken.None), Throws.ArgumentNullException);
    }

    [Test]
    public void LogInCommand_ToString_HasTheEmailButNotThePassword()
    {
        // Arrange
        var command = new LogInCommand(Email, Password);

        // Act
        string text = command.ToString();

        // Assert
        Assert.That(text, Is.EqualTo("LogInCommand { Email = anna@fitlife.example }"));
    }
}
