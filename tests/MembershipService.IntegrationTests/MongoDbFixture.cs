using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Testcontainers.MongoDb;

namespace MembershipService.IntegrationTests;

/// <summary>
/// A MongoDB in a container, with the database <c>membership</c> and its own user <c>membership-svc</c>,
/// as <c>deploy/compose/mongodb/init/01-service-users.js</c> creates them.
/// </summary>
public sealed class MongoDbFixture : IAsyncDisposable
{
    /// <summary>The password of the test database user.</summary>
    public const string ServicePassword = "membership-test-password";

    private readonly MongoDbContainer _container;

    /// <summary>A MongoDB with compose's image, unless the environment variable MONGO_IMAGE names another.</summary>
    public MongoDbFixture()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    /// <summary>
    /// A MongoDB with the image compose uses (deploy/compose/infrastructure.yml). MONGO_IMAGE overrides it the same way, for a
    /// Docker whose kernel MongoDB 8 refuses (deploy/compose/README.md), as in the other services' fixtures (#588).
    /// </summary>
    /// <param name="environment">Where MONGO_IMAGE is read from.</param>
    public MongoDbFixture(IConfiguration environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _container = new MongoDbBuilder(environment["MONGO_IMAGE"] ?? "mongo:8.0.38").Build();
    }

    /// <summary>The image the container runs, e.g. <c>mongo:8.0.38</c>.</summary>
    public string Image => _container.Image.FullName;

    /// <summary>The host the container is reached on.</summary>
    public string Host => _container.Hostname;

    /// <summary>The port MongoDB is published on.</summary>
    public int Port => _container.GetMappedPublicPort(MongoDbBuilder.MongoDbPort);

    /// <summary>Starts the container and creates the service's user.</summary>
    public async Task StartAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        ExecResult result = await _container.ExecScriptAsync(
            $"db.getSiblingDB('membership').createUser({{ user: 'membership-svc', pwd: '{ServicePassword}', roles: [{{ role: 'readWrite', db: 'membership' }}] }})")
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Found no membership-svc user after the create script: " + result.Stderr);
        }
    }

    /// <summary>Runs a mongosh script as the container's root user, for state the service would never write itself.</summary>
    public async Task RunScriptAsync(string script)
    {
        ExecResult result = await _container.ExecScriptAsync(script).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("The mongosh script failed: " + result.Stderr);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _container.DisposeAsync();
    }
}
