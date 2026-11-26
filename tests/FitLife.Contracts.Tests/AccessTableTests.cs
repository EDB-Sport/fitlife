using FitLife.Authentication;
using FitLife.Contracts.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FitLife.Contracts.Tests;

/// <summary>
/// <see cref="AccessTable"/> finds every way a service's rows in ACCESS.md can differ from its endpoints (#489).
/// Each test builds a small app in-process and reads its endpoints; the app is not started.
/// </summary>
public sealed class AccessTableTests
{
    private const string Service = "TestService";

    [Test]
    public async Task Endpoint_WithNoRow_IsReported()
    {
        // Arrange
        await using WebApplication app = App(endpoints => endpoints.MapGet("/things", Ok));
        string table = Table();

        // Act
        IReadOnlyList<string> differences = await CompareAsync(table, app).ConfigureAwait(false);

        // Assert
        Assert.That(differences, Has.One.Contains("GET /things"));
    }

    [Test]
    public async Task Row_WithNoEndpoint_IsReported()
    {
        // Arrange
        await using WebApplication app = App(_ => { });
        string table = Table("| `GET /things` | TestService | – | yes | yes | yes | #1 |");

        // Act
        IReadOnlyList<string> differences = await CompareAsync(table, app).ConfigureAwait(false);

        // Assert
        Assert.That(differences, Has.One.Contains("GET /things"));
    }

    [Test]
    public async Task PlannedRow_WithNoEndpoint_IsSkipped()
    {
        // Arrange
        await using WebApplication app = App(_ => { });
        string table = Table("| `GET /things` (planned) | TestService | – | yes | yes | yes | #1 |");

        // Act
        IReadOnlyList<string> differences = await CompareAsync(table, app).ConfigureAwait(false);

        // Assert
        Assert.That(differences, Is.Empty);
    }

    [Test]
    public async Task RowOfAnotherService_IsSkipped()
    {
        // Arrange
        await using WebApplication app = App(_ => { });
        string table = Table("| `GET /things` | OtherService | – | yes | yes | yes | #1 |");

        // Act
        IReadOnlyList<string> differences = await CompareAsync(table, app).ConfigureAwait(false);

        // Assert
        Assert.That(differences, Is.Empty);
    }

    [Test]
    public async Task RoleCell_ThatDoesNotMatchTheRoles_IsReported()
    {
        // Arrange
        await using WebApplication app = App(endpoints => endpoints.MapGet("/things", Ok).RequireAuthorization(FitLifePolicies.Admin));
        string table = Table("| `GET /things` | TestService | – | yes | yes | yes | #1 |");

        // Act
        IReadOnlyList<string> differences = await CompareAsync(table, app).ConfigureAwait(false);

        // Assert
        Assert.That(differences, Has.One.Contains("GET /things"));
    }

    [Test]
    public async Task OpenCell_OnAnEndpointThatNeedsAToken_IsReported()
    {
        // Arrange
        await using WebApplication app = App(endpoints => endpoints.MapGet("/things", Ok));
        string table = Table("| `GET /things` | TestService | yes | yes | yes | yes | #1 |");

        // Act
        IReadOnlyList<string> differences = await CompareAsync(table, app).ConfigureAwait(false);

        // Assert
        Assert.That(differences, Has.One.Contains("GET /things"));
    }

    [Test]
    public async Task MatchingRows_AreNotReported()
    {
        // Arrange
        await using WebApplication app = App(endpoints =>
        {
            endpoints.MapPost("/things", Ok).AllowAnonymous();
            endpoints.MapGet("/things/{id:guid}", Ok);
            endpoints.MapDelete("/things/{id:guid}", Ok).RequireAuthorization(FitLifePolicies.TraenerOrAdmin);
            endpoints.MapPut("/things/{id:guid}/owner", Ok).RequireAuthorization(new AuthorizeAttribute { Roles = FitLifeRoles.Medlem });
        });
        string table = Table(
            "| `POST /things` | TestService | yes | yes | yes | yes | #1 |",
            "| `GET /things/{id}` | TestService | – | yes | yes | yes | #1 |",
            "| `DELETE /things/{id}` | TestService | – | – | yes | yes | #1 |",
            "| `PUT /things/{id}/owner` | TestService | – | yes | – | – | #1 |");

        // Act
        IReadOnlyList<string> differences = await CompareAsync(table, app).ConfigureAwait(false);

        // Assert
        Assert.That(differences, Is.Empty);
    }

    /// <summary>An app with the FitLife policies and closed by default, as Identity and Membership are.</summary>
    private static WebApplication App(Action<WebApplication> mapEndpoints)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddFitLifeAuthorization();
        WebApplication app = builder.Build();
        mapEndpoints(app);
        return app;
    }

    /// <summary>The app's endpoints are read from its route builder: the app is not started, so no host runs.</summary>
    private static Task<IReadOnlyList<string>> CompareAsync(string table, WebApplication app)
    {
        IEnumerable<Endpoint> endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints);
        IAuthorizationPolicyProvider policies = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        return AccessTable.CompareAsync(Service, table, endpoints, policies);
    }

    private static string Table(params string[] rows)
    {
        return string.Join('\n', [
            "| Endpoint or page | Service | Open | Medlem | Træner | Admin | Issue |",
            "|---|---|---|---|---|---|---|",
            .. rows,
        ]);
    }

    private static IResult Ok()
    {
        return Results.Ok();
    }
}
