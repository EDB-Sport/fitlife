using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MembershipService.IntegrationTests;

/// <summary>Course rule #7: the OpenAPI document and Scalar exist in Development only.</summary>
public sealed class ApiDocumentationTests
{
    [TestCase("/openapi/v1.json")]
    [TestCase("/scalar/v1")]
    public async Task Get_InProduction_ReturnsNotFound(string path)
    {
        // Arrange
        using var app = new MembershipFactory();
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
        using var app = new MembershipFactory();
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host.UseEnvironment("Development"));
        using HttpClient client = factory.CreateClient();

        // Act
        using HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("JoinMembership", "The account in the token joins FitLife and gets an active membership")]
    [TestCase("GetMemberByAccount", "The member id of an account, and whether its membership is active")]
    public async Task OpenApi_InDevelopment_DescribesTheMemberOperations(string operationId, string summary)
    {
        // Arrange
        using var app = new MembershipFactory();
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host.UseEnvironment("Development"));
        using HttpClient client = factory.CreateClient();

        // Act
        string document = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(document, Does.Contain($"\"operationId\": \"{operationId}\"").And.Contain($"\"summary\": \"{summary}\""));
    }
}
