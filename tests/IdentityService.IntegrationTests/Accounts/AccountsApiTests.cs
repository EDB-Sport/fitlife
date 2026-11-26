using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityService.Application;
using IdentityService.Domain;

namespace IdentityService.IntegrationTests.Accounts;

/// <summary>POST /accounts (#234): a person creates an account with an e-mail address and a password, and gets the role Medlem.</summary>
public sealed class AccountsApiTests
{
    private const string Email = "anna@fitlife.example";
    private const string Password = "made-up-password-1";

    private static readonly Uri Accounts = new("/accounts", UriKind.Relative);
    private static readonly string[] OnlyMedlem = ["Medlem"];

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string email, string password)
    {
        return client.PostAsJsonAsync(Accounts, new { email, password });
    }

    [Test]
    public async Task PostAccounts_WithANewEmail_Returns201()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await PostAsync(client, Email, Password).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        Account stored = factory.Repository.Accounts.Single();
        Assert.That(body.RootElement.GetProperty("id").GetGuid(), Is.EqualTo(stored.Id));
        Assert.That(body.RootElement.GetProperty("email").GetString(), Is.EqualTo(Email));
        Assert.That(body.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()), Is.EqualTo(OnlyMedlem));
    }

    [TestCase("not-an-address", Password)]
    [TestCase("", Password)]
    [TestCase(Email, "short-7")]
    [TestCase(Email, "")]
    public async Task PostAccounts_WithAnInvalidEmailOrAShortPassword_Returns400(string email, string password)
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await PostAsync(client, email, password).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(factory.Repository.Accounts, Is.Empty);
    }

    [Test]
    public async Task PostAccounts_WithAPasswordOfEightCharacters_Returns201()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await PostAsync(client, Email, "eight-ch").ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [TestCase(Email)]
    [TestCase("ANNA@FitLife.Example")]
    public async Task PostAccounts_WithATakenEmail_Returns409WithTheCode(string sameEmail)
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage first = await PostAsync(client, Email, Password).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await PostAsync(client, sameEmail, "another-password").ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        Assert.That(body.RootElement.GetProperty("code").GetString(), Is.EqualTo("EmailAlreadyTaken"));
        Assert.That(body.RootElement.GetProperty("title").GetString(), Is.EqualTo("The e-mail address is taken."));
        Assert.That(factory.Repository.Accounts, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task PostAccounts_Response_HasNoPasswordOrHash()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await PostAsync(client, Email, Password).ConfigureAwait(false);

        // Assert
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        string hash = factory.Repository.Accounts.Single().PasswordHash;
        Assert.That(body, Does.Not.Contain(Password));
        Assert.That(body, Does.Not.Contain(hash));
        Assert.That(body, Does.Not.Contain("password").IgnoreCase);
    }

    [Test]
    public async Task PostAccounts_LogLines_HaveNoPasswordOrHash()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await PostAsync(client, Email, Password).ConfigureAwait(false);

        // Assert
        string hash = factory.Repository.Accounts.Single().PasswordHash;
        Assert.That(factory.Logs.Lines, Is.Not.Empty);
        Assert.That(factory.Logs.Lines, Has.None.Contains(Password));
        Assert.That(factory.Logs.Lines, Has.None.Contains(hash));
    }

    [Test]
    public async Task PostAccounts_WithANewEmail_LogsTheNewAccountsId()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await PostAsync(client, Email, Password).ConfigureAwait(false);

        // Assert
        Guid id = factory.Repository.Accounts.Single().Id;
        Assert.That(factory.Logs.Lines, Has.Some.StartsWith($"Account {id} created"));
    }

    [Test]
    public async Task PostAccounts_WhenTheRepositoryFails_Returns500ProblemWithoutTheExceptionText()
    {
        // Arrange
        using var factory = new IdentityApiFactory(new FailingAccountRepository());
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await PostAsync(client, Email, Password).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        Assert.That(await response.Content.ReadAsStringAsync().ConfigureAwait(false), Does.Not.Contain(FailingAccountRepository.Message));
    }

    private sealed class FailingAccountRepository : IAccountRepository
    {
        public const string Message = "the database is on fire";

        public Task<bool> AddAsync(Account account, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(Message);
        }

        public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(Message);
        }

        public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(Message);
        }

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(Message);
        }

        public Task<bool> SaveRolesAsync(Account account, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(Message);
        }
    }
}
