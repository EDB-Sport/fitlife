using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FitLife.Authentication;
using IdentityService.Domain;

namespace IdentityService.IntegrationTests.Accounts;

/// <summary>GET /accounts (#364): an Admin sees every account with its id, e-mail address and roles. Identity's first endpoint that needs a token.</summary>
public sealed class ListAccountsApiTests
{
    private const string Hash = "AQAAAAIAAYagAAAAEhash";

    private static readonly Uri Accounts = new("/accounts", UriKind.Relative);
    private static readonly string[] EmailsInOrder = ["admin@fitlife.example", "anna@fitlife.example", "bo@fitlife.example"];
    private static readonly string[][] RolesInOrder = [["Admin"], ["Medlem"], ["Træner"]];

    private static async Task<IdentityApiFactory> FactoryWithThreeAccountsAsync()
    {
        var factory = new IdentityApiFactory();
        await factory.Repository.AddAsync(Account.Create("bo@fitlife.example", Hash, Role.Traener), CancellationToken.None).ConfigureAwait(false);
        await factory.Repository.AddAsync(Account.Create("admin@fitlife.example", Hash, Role.Admin), CancellationToken.None).ConfigureAwait(false);
        await factory.Repository.AddAsync(Account.Create("anna@fitlife.example", Hash), CancellationToken.None).ConfigureAwait(false);
        return factory;
    }

    private static HttpClient ClientWithRole(IdentityApiFactory factory, string role)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(role));
        return client;
    }

    [Test]
    public async Task GetAccounts_AsAdmin_ReturnsEveryAccount()
    {
        // Arrange
        using IdentityApiFactory factory = await FactoryWithThreeAccountsAsync().ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.GetAsync(Accounts).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        JsonElement[] listed = [.. body.RootElement.EnumerateArray()];
        Assert.That(listed.Select(account => account.GetProperty("email").GetString()), Is.EqualTo(EmailsInOrder));
        Assert.That(listed.Select(account => account.GetProperty("id").GetGuid()), Is.EqualTo(listed.Select(account => FindId(factory, account.GetProperty("email").GetString()))));
        Assert.That(listed.Select(account => account.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ToArray()), Is.EqualTo(RolesInOrder));
    }

    [Test]
    public async Task GetAccounts_WithoutAToken_Returns401()
    {
        // Arrange
        using IdentityApiFactory factory = await FactoryWithThreeAccountsAsync().ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(Accounts).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [TestCase(FitLifeRoles.Medlem)]
    [TestCase(FitLifeRoles.Traener)]
    public async Task GetAccounts_WithoutTheAdminRole_Returns403(string role)
    {
        // Arrange
        using IdentityApiFactory factory = await FactoryWithThreeAccountsAsync().ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, role);

        // Act
        using HttpResponseMessage response = await client.GetAsync(Accounts).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetAccounts_Response_HasNoPasswordOrHash()
    {
        // Arrange
        using IdentityApiFactory factory = await FactoryWithThreeAccountsAsync().ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.GetAsync(Accounts).ConfigureAwait(false);

        // Assert
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.That(body, Does.Not.Contain(Hash));
        Assert.That(body, Does.Not.Contain("password").IgnoreCase);
        Assert.That(body, Does.Not.Contain("hash").IgnoreCase);
    }

    private static Guid FindId(IdentityApiFactory factory, string? email)
    {
        return factory.Repository.Accounts.Single(account => string.Equals(account.Email, email, StringComparison.Ordinal)).Id;
    }
}
