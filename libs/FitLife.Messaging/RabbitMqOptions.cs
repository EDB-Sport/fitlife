namespace FitLife.Messaging;

/// <summary>Where RabbitMQ is: the <c>RabbitMq</c> configuration section (in compose, <c>RabbitMq__HostName</c> and so on).</summary>
public sealed class RabbitMqOptions
{
    /// <summary>The configuration section.</summary>
    public const string Section = "RabbitMq";

    /// <summary>Gets or sets the host name, e.g. <c>rabbitmq</c> in compose.</summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>Gets or sets the AMQP port.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>Gets or sets the user name.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the password. From .env or Vault, never from appsettings.json.</summary>
    public string Password { get; set; } = string.Empty;
}
