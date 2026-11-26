using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BookingService.IntegrationTests;

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

    [Test]
    public async Task Get_OpenApiDocument_RequiresABearerTokenOnTheThreeMedlemEndpointsOnly()
    {
        // Arrange
        using var app = new WebApplicationFactory<Program>();
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host.UseEnvironment("Development"));
        using HttpClient client = factory.CreateClient();

        // Act
        var document = JsonNode.Parse(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)).ConfigureAwait(false));

        // Assert
        JsonNode? paths = document?["paths"];
        Assert.Multiple(() =>
        {
            Assert.That(RequiresBearer(paths?["/class-sessions/{id}/reservations"]?["post"]), Is.True, "POST reservation");
            Assert.That(RequiresBearer(paths?["/class-sessions/{id}/reservations/{memberId}"]?["delete"]), Is.True, "DELETE reservation");
            Assert.That(RequiresBearer(paths?["/class-sessions/{id}/waitlist"]?["post"]), Is.True, "POST waitlist");
            Assert.That(paths?["/class-sessions"]?["get"], Is.Not.Null, "GET class schedule is in the document");
            Assert.That(RequiresBearer(paths?["/class-sessions"]?["get"]), Is.False, "GET class schedule");
        });
    }

    private static bool RequiresBearer(JsonNode? operation)
    {
        return operation?["security"] is JsonArray security && security.Any(requirement => requirement is JsonObject scheme && scheme.ContainsKey("Bearer"));
    }
}
