using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FitLife.Authentication;
using IdentityService.Domain;
using Microsoft.IdentityModel.JsonWebTokens;

namespace IdentityService.IntegrationTests.Accounts;

/// <summary>PUT and DELETE /accounts/{id}/roles/traener (#365): an Admin gives or takes the role Træner. Both are idempotent.</summary>
public sealed class AccountRolesApiTests
{
    private const string Hash = "AQAAAAIAAYagAAAAEhash";
    private const string Email = "anna@fitlife.example";
    private const string Password = "made-up-password-1";

    [Test]
    public async Task PutTraener_AsAdmin_GivesTheRole()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        Account account = await StoredAsync(factory, Account.Create(Email, Hash)).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.PutAsync(TraenerOf(account.Id), content: null).ConfigureAwait(false);

        // Assert
        Assert.That((response.StatusCode, RolesOf(factory, account.Id)), Is.EqualTo((HttpStatusCode.NoContent, new[] { Role.Medlem, Role.Traener })));
    }

    [Test]
    public async Task PutTraener_Twice_ChangesNothing()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        Account account = await StoredAsync(factory, Account.Create(Email, Hash)).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);
        using HttpResponseMessage first = await client.PutAsync(TraenerOf(account.Id), content: null).ConfigureAwait(false);

        // Act
        using HttpResponseMessage second = await client.PutAsync(TraenerOf(account.Id), content: null).ConfigureAwait(false);

        // Assert
        Assert.That((second.StatusCode, RolesOf(factory, account.Id)), Is.EqualTo((HttpStatusCode.NoContent, new[] { Role.Medlem, Role.Traener })));
    }

    [Test]
    public async Task DeleteTraener_AsAdmin_TakesTheRole()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        var account = Account.Create(Email, Hash);
        account.GiveRole(Role.Traener);
        await StoredAsync(factory, account).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(TraenerOf(account.Id)).ConfigureAwait(false);

        // Assert
        Assert.That((response.StatusCode, RolesOf(factory, account.Id)), Is.EqualTo((HttpStatusCode.NoContent, new[] { Role.Medlem })));
    }

    [Test]
    public async Task DeleteTraener_WhenTheAccountNeverHadIt_Returns204()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        Account account = await StoredAsync(factory, Account.Create(Email, Hash)).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(TraenerOf(account.Id)).ConfigureAwait(false);

        // Assert
        Assert.That((response.StatusCode, RolesOf(factory, account.Id)), Is.EqualTo((HttpStatusCode.NoContent, new[] { Role.Medlem })));
    }

    [Test]
    public async Task DeleteTraener_TheLastRole_Returns409()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        Account account = await StoredAsync(factory, Account.Create(Email, Hash, Role.Traener)).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(TraenerOf(account.Id)).ConfigureAwait(false);

        // Assert
        Assert.That((response.StatusCode, response.Content.Headers.ContentType?.MediaType, RolesOf(factory, account.Id)), Is.EqualTo((HttpStatusCode.Conflict, "application/problem+json", new[] { Role.Traener })));
    }

    [Test]
    public async Task PutTraener_ForAnUnknownAccount_Returns404ProblemDetails()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.PutAsync(TraenerOf(Guid.NewGuid()), content: null).ConfigureAwait(false);

        // Assert
        Assert.That((response.StatusCode, response.Content.Headers.ContentType?.MediaType), Is.EqualTo((HttpStatusCode.NotFound, "application/problem+json")));
    }

    [Test]
    public async Task DeleteTraener_ForAnUnknownAccount_Returns404ProblemDetails()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Admin);

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(TraenerOf(Guid.NewGuid())).ConfigureAwait(false);

        // Assert
        Assert.That((response.StatusCode, response.Content.Headers.ContentType?.MediaType), Is.EqualTo((HttpStatusCode.NotFound, "application/problem+json")));
    }

    [Test]
    public async Task PutTraener_WithoutAToken_Returns401()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        Account account = await StoredAsync(factory, Account.Create(Email, Hash)).ConfigureAwait(false);
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.PutAsync(TraenerOf(account.Id), content: null).ConfigureAwait(false);

        // Assert
        Assert.That((response.StatusCode, RolesOf(factory, account.Id)), Is.EqualTo((HttpStatusCode.Unauthorized, new[] { Role.Medlem })));
    }

    [TestCase(FitLifeRoles.Medlem)]
    [TestCase(FitLifeRoles.Traener)]
    public async Task PutTraener_WithoutAdmin_Returns403(string role)
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        Account account = await StoredAsync(factory, Account.Create(Email, Hash)).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, role);

        // Act
        using HttpResponseMessage response = await client.PutAsync(TraenerOf(account.Id), content: null).ConfigureAwait(false);

        // Assert
        Assert.That((response.StatusCode, RolesOf(factory, account.Id)), Is.EqualTo((HttpStatusCode.Forbidden, new[] { Role.Medlem })));
    }

    [Test]
    public async Task DeleteTraener_WithoutAdmin_Returns403()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        var account = Account.Create(Email, Hash);
        account.GiveRole(Role.Traener);
        await StoredAsync(factory, account).ConfigureAwait(false);
        using HttpClient client = ClientWithRole(factory, FitLifeRoles.Traener);

        // Act
        using HttpResponseMessage response = await client.DeleteAsync(TraenerOf(account.Id)).ConfigureAwait(false);

        // Assert
        Assert.That((response.StatusCode, RolesOf(factory, account.Id)), Is.EqualTo((HttpStatusCode.Forbidden, new[] { Role.Medlem, Role.Traener })));
    }

    [Test]
    public async Task PutTraener_ThenLogIn_TheTokenHasTheTraenerRole()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        using HttpClient anonymous = factory.CreateClient();
        using HttpResponseMessage created = await anonymous.PostAsJsonAsync(new Uri("/accounts", UriKind.Relative), new { email = Email, password = Password }).ConfigureAwait(false);
        Guid id = factory.Repository.Accounts.Single().Id;
        using HttpClient admin = ClientWithRole(factory, FitLifeRoles.Admin);
        using HttpResponseMessage given = await admin.PutAsync(TraenerOf(id), content: null).ConfigureAwait(false);

        // Act
        using HttpResponseMessage response = await anonymous.PostAsJsonAsync(new Uri("/tokens", UriKind.Relative), new { email = Email, password = Password }).ConfigureAwait(false);

        // Assert
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        var token = new JsonWebToken(body.RootElement.GetProperty("accessToken").GetString());
        Assert.That(token.Claims.Where(claim => string.Equals(claim.Type, FitLifeJwt.RoleClaim, StringComparison.Ordinal)).Select(claim => claim.Value), Is.EquivalentTo(new[] { FitLifeRoles.Medlem, FitLifeRoles.Traener }));
    }

    [Test]
    public async Task PutTraener_LogsBothAccountIdsAndNoEmail()
    {
        // Arrange
        using var factory = new IdentityApiFactory();
        Account account = await StoredAsync(factory, Account.Create(Email, Hash)).ConfigureAwait(false);
        string adminAccountId = Guid.NewGuid().ToString();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(FitLifeRoles.Admin, adminAccountId));

        // Act
        using HttpResponseMessage response = await client.PutAsync(TraenerOf(account.Id), content: null).ConfigureAwait(false);

        // Assert
        string[] changes = [.. factory.Logs.Lines.Where(line => line.Contains(account.Id.ToString(), StringComparison.Ordinal))];
        Assert.Multiple(() =>
        {
            Assert.That(changes, Has.Exactly(1).Contains(adminAccountId).And.Contains("Træner"));
            Assert.That(factory.Logs.Lines, Has.None.Contains(Email));
        });
    }

    private static async Task<Account> StoredAsync(IdentityApiFactory factory, Account account)
    {
        await factory.Repository.AddAsync(account, CancellationToken.None).ConfigureAwait(false);
        return account;
    }

    private static Role[] RolesOf(IdentityApiFactory factory, Guid id)
    {
        return [.. factory.Repository.Accounts.Single(account => account.Id == id).Roles];
    }

    private static HttpClient ClientWithRole(IdentityApiFactory factory, string role)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(role));
        return client;
    }

    private static Uri TraenerOf(Guid id)
    {
        return new Uri($"/accounts/{id}/roles/traener", UriKind.Relative);
    }
}
