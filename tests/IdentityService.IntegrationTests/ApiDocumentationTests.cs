using System.Net;
using System.Text.Json;
using IdentityService.IntegrationTests.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IdentityService.IntegrationTests;

/// <summary>Course rule #7: the OpenAPI document and Scalar exist in Development only.</summary>
public sealed class ApiDocumentationTests
{
    [TestCase("/openapi/v1.json")]
    [TestCase("/scalar/v1")]
    public async Task Get_InProduction_ReturnsNotFound(string path)
    {
        // Arrange
        using var app = new WebApplicationFactory<Program>();
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host.UseEnvironment("Production"));
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task OpenApiDocument_GetAccounts_RequiresABearerToken()
    {
        // Arrange
        using JsonDocument document = await OpenApiDocumentAsync().ConfigureAwait(false);

        // Act
        JsonElement operation = document.RootElement.GetProperty("paths").GetProperty("/accounts").GetProperty("get");

        // Assert
        Assert.That(operation.GetProperty("security").GetRawText(), Does.Contain("Bearer"));
        Assert.That(document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString(), Is.EqualTo("bearer"));
    }

    [TestCase("/accounts")]
    [TestCase("/tokens")]
    public async Task OpenApiDocument_Post_RequiresNoToken(string path)
    {
        // Arrange
        using JsonDocument document = await OpenApiDocumentAsync().ConfigureAwait(false);

        // Act
        JsonElement operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("post");

        // Assert
        Assert.That(operation.TryGetProperty("security", out _), Is.False);
    }

    [TestCase("/openapi/v1.json")]
    [TestCase("/scalar/v1")]
    public async Task Get_InDevelopment_ReturnsOk(string path)
    {
        // Arrange
        using var app = new WebApplicationFactory<Program>();
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host.UseEnvironment("Development"));
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static async Task<JsonDocument> OpenApiDocumentAsync()
    {
        using var app = new WebApplicationFactory<Program>();
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host.UseEnvironment("Development").UseSetting("Jwt:SigningKey", IdentityApiFactory.SigningKey));
        using HttpClient client = factory.CreateClient();
        string json = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)).ConfigureAwait(false);
        return JsonDocument.Parse(json);
    }
}
