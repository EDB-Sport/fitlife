using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace IdentityService.Tests.Application;

/// <summary>A rejected log-in leaves a trace in the logs, without the e-mail address, the password, the hash or the token (#367).</summary>
public sealed class LogInHandlerLoggingTests
{
    private const string Email = "anna@fitlife.example";
    private const string Password = "made-up-password-1";

    private readonly PasswordHasher<Account> _hasher = new();

    [Test]
    public async Task Handle_WithAWrongPassword_LogsLogInRejectedWithTheAccountId()
    {
        // Arrange
        Account account = StoredAccount();
        var logger = new CapturingLogger();

        // Act
        await LogIn(account, "not-the-password", logger).ConfigureAwait(false);

        // Assert
        LogLine line = logger.Lines.Single();
        Assert.That(line.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(line.EventName, Is.EqualTo("LogInRejected"));
        Assert.That(line.Property("Reason"), Is.EqualTo("WrongPassword"));
        Assert.That(line.Property("AccountId"), Is.EqualTo(account.Id.ToString()));
    }

    [Test]
    public async Task Handle_WithAnUnknownEmail_LogsLogInRejected()
    {
        // Arrange
        var logger = new CapturingLogger();

        // Act
        await LogIn(account: null, Password, logger).ConfigureAwait(false);

        // Assert
        LogLine line = logger.Lines.Single();
        Assert.That(line.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(line.EventName, Is.EqualTo("LogInRejected"));
        Assert.That(line.Property("Reason"), Is.EqualTo("UnknownEmail"));
        Assert.That(line.Properties.Keys, Has.No.Member("AccountId"));
    }

    [Test]
    public async Task Handle_WithTheRightPassword_LogsLoggedIn()
    {
        // Arrange
        Account account = StoredAccount();
        var logger = new CapturingLogger();

        // Act
        await LogIn(account, Password, logger).ConfigureAwait(false);

        // Assert
        LogLine line = logger.Lines.Single();
        Assert.That(line.Level, Is.EqualTo(LogLevel.Information));
        Assert.That(line.EventName, Is.EqualTo("LoggedIn"));
        Assert.That(line.Property("AccountId"), Is.EqualTo(account.Id.ToString()));
    }

    [TestCase(true, Password)]
    [TestCase(true, "not-the-password")]
    [TestCase(false, Password)]
    public async Task LogIn_LogLines_HaveNoEmailPasswordOrToken(bool accountExists, string password)
    {
        // Arrange
        Account? account = null;
        if (accountExists)
        {
            account = StoredAccount();
        }
        var logger = new CapturingLogger();

        // Act
        LogInResult? result = await LogIn(account, password, logger).ConfigureAwait(false);

        // Assert
        Assert.That(logger.Lines, Is.Not.Empty);
        foreach (string text in logger.Lines.SelectMany(line => line.Texts()))
        {
            Assert.That(text, Does.Not.Contain(Email).IgnoreCase);
            Assert.That(text, Does.Not.Contain(password));
            if (account is not null)
            {
                Assert.That(text, Does.Not.Contain(account.PasswordHash));
            }

            if (result is not null)
            {
                Assert.That(text, Does.Not.Contain(result.AccessToken));
            }
        }
    }

    private Account StoredAccount()
    {
        // ! PasswordHasher<TUser> doesn't read the user.
        return Account.Create(Email, _hasher.HashPassword(null!, Password));
    }

    private Task<LogInResult?> LogIn(Account? account, string password, CapturingLogger logger)
    {
        var repository = new Mock<IAccountRepository>();
        repository.Setup(accounts => accounts.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(account);
        return LogInHandlerTests.Handler(repository, _hasher, logger).HandleAsync(new LogInCommand(Email, password), CancellationToken.None);
    }

    private sealed record LogLine(LogLevel Level, string? EventName, string Message, IReadOnlyDictionary<string, string?> Properties)
    {
        public string? Property(string name)
        {
            Properties.TryGetValue(name, out string? value);
            return value;
        }

        public IEnumerable<string> Texts()
        {
            return Properties.Values.OfType<string>().Append(Message);
        }
    }

    private sealed class CapturingLogger : ILogger<LogInHandler>
    {
        public List<LogLine> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach ((string key, object? value) in values)
                {
                    properties[key] = value?.ToString();
                }
            }

            Lines.Add(new LogLine(logLevel, eventId.Name, formatter(state, exception), properties));
        }
    }
}
