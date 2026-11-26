using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FitLife.Authentication;
using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace IdentityService.IntegrationTests.Accounts;

/// <summary>The demo accounts (#237): with <c>Seed:DemoAccounts</c> true, Identity seeds one account per role after start-up.</summary>
public sealed class DemoAccountsApiTests
{
    private static readonly Uri Health = new("/health", UriKind.Relative);
    private static readonly Uri Tokens = new("/tokens", UriKind.Relative);
    private static readonly TimeSpan SeedTimeout = TimeSpan.FromSeconds(30);

    private static void TurnOnTheSeed(IdentityApiFactory factory)
    {
        factory.Settings["Seed:DemoAccounts"] = "true";
        factory.Settings["Seed:MedlemPassword"] = "change-me-demo-medlem";
        factory.Settings["Seed:TraenerPassword"] = "change-me-demo-traener";
        factory.Settings["Seed:AdminPassword"] = "change-me-demo-admin";
    }

    [Test]
    public async Task Health_UntilTheSeedHasFinished_IsUnhealthy()
    {
        // Arrange
        var repository = new GatedAccountRepository();
        using var factory = new IdentityApiFactory(repository);
        TurnOnTheSeed(factory);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage whileSeeding = await client.GetAsync(Health).ConfigureAwait(false);
        repository.Open();
        await factory.Services.GetRequiredService<DemoAccountSeedStatus>().Finished.WaitAsync(SeedTimeout).ConfigureAwait(false);
        using HttpResponseMessage afterSeeding = await client.GetAsync(Health).ConfigureAwait(false);

        // Assert
        Assert.That(whileSeeding.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(afterSeeding.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("medlem@fitlife.example", "change-me-demo-medlem", "Medlem")]
    [TestCase("traener@fitlife.example", "change-me-demo-traener", "Træner")]
    [TestCase("admin@fitlife.example", "change-me-demo-admin", "Admin")]
    public async Task PostTokens_ForASeededDemoAccount_ReturnsATokenWithItsRole(string email, string password, string role)
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        TurnOnTheSeed(factory);
        using HttpClient client = factory.CreateClient();
        await factory.Services.GetRequiredService<DemoAccountSeedStatus>().Finished.WaitAsync(SeedTimeout).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync(Tokens, new { email, password }).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        var token = new JsonWebToken(body.RootElement.GetProperty("accessToken").GetString());
        Assert.That(
            token.Claims.Where(claim => string.Equals(claim.Type, FitLifeJwt.RoleClaim, StringComparison.Ordinal)).Select(claim => claim.Value),
            Is.EqualTo(new[] { role }));
    }

    /// <summary>Accounts in memory, but every call waits until <see cref="Open"/>, so the test can look at the app while the seed runs.</summary>
    private sealed class GatedAccountRepository : IAccountRepository
    {
        private readonly InMemoryAccountRepository _accounts = new();
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Open()
        {
            _gate.SetResult();
        }

        public async Task<bool> AddAsync(Account account, CancellationToken cancellationToken)
        {
            await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return await _accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return await _accounts.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken)
        {
            await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return await _accounts.ListAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return _accounts.FindByIdAsync(id, cancellationToken);
        }

        public Task<bool> SaveRolesAsync(Account account, CancellationToken cancellationToken)
        {
            return _accounts.SaveRolesAsync(account, cancellationToken);
        }
    }
}
