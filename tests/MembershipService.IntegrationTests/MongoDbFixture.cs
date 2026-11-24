using DotNet.Testcontainers.Containers;
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

    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:8.0.38").Build();

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

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _container.DisposeAsync();
    }
}
