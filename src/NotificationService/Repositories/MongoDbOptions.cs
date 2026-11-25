namespace NotificationService.Repositories;

/// <summary>Where the service's database is: the <c>MongoDb</c> configuration section.</summary>
public sealed class MongoDbOptions
{
    /// <summary>The configuration section.</summary>
    public const string Section = "MongoDb";

    /// <summary>Gets or sets the host name, e.g. <c>mongodb</c> in compose.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Gets or sets the port.</summary>
    public int Port { get; set; } = 27017;

    /// <summary>Gets or sets the service's own database (one bounded context = one database).</summary>
    public string Database { get; set; } = "notification";

    /// <summary>Gets or sets the user, <c>notification-svc</c> (deploy/compose/mongodb/init).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the password. From .env or Vault, never from appsettings.json.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the database the user is defined in; the service's own database when not set.</summary>
    public string? AuthDatabase { get; set; }
}
