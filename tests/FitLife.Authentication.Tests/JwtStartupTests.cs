using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitLife.Authentication.Tests;

/// <summary>A missing or too short signing key stops the service when the host starts, not on the first request (#183).</summary>
public sealed class JwtStartupTests
{
    private const string ShortKey = "too-short-key";

    [Test]
    public void Start_WithoutASigningKey_FailsAndNamesTheConfigurationKey()
    {
        // Arrange
        using IHost host = BuildHost(signingKey: null, entryAssemblyName: "MembershipService");

        // Act
        Func<Task> act = () => host.StartAsync();

        // Assert
        Assert.That(act, Throws.InvalidOperationException.With.Message.Contains("Jwt:SigningKey"));
    }

    [Test]
    public void Start_WithAShortSigningKey_FailsWithoutShowingTheKey()
    {
        // Arrange
        using IHost host = BuildHost(ShortKey, entryAssemblyName: "MembershipService");

        // Act
        Func<Task> act = () => host.StartAsync();

        // Assert
        Assert.That(act, Throws.InvalidOperationException
            .With.Message.Contains("Jwt:SigningKey")
            .And.Message.Not.Contains(ShortKey));
    }

    [Test]
    public async Task Start_DuringBuildTimeOpenApiGeneration_StartsWithoutASigningKey()
    {
        // Arrange: dotnet-getdocument starts the app as GetDocument.Insider, without Vault
        using IHost host = BuildHost(signingKey: null, entryAssemblyName: "GetDocument.Insider");

        // Act
        Exception? thrown = null;
        try
        {
            await host.StartAsync();
        }
        catch (InvalidOperationException exception)
        {
            thrown = exception;
        }
        finally
        {
            await host.StopAsync();
        }

        // Assert
        Assert.That(thrown, Is.Null);
    }

    private static IHost BuildHost(string? signingKey, string entryAssemblyName)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Jwt:SigningKey"] = signingKey });
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        builder.Services.AddFitLifeJwtAuthentication(entryAssemblyName);
        return builder.Build();
    }
}
