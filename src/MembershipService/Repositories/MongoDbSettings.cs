namespace MembershipService.Repositories;

/// <summary>
/// Where the service's database is, from configuration section <c>MongoDb</c>. The password comes from Vault
/// (<c>MongoDb:Password</c>, deploy/compose/README.md), the rest from appsettings.json or the compose environment.
/// </summary>
public sealed class MongoDbSettings
{
    /// <summary>The configuration section.</summary>
    public const string Section = "MongoDb";

    /// <summary>The MongoDB host.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>The MongoDB port.</summary>
    public int Port { get; init; }

    /// <summary>The service's own database, also the one its user is created in.</summary>
    public string Database { get; init; } = string.Empty;

    /// <summary>The service's own database user.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>The user's password.</summary>
    public string Password { get; init; } = string.Empty;
}
