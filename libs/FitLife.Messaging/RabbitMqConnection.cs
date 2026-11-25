using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FitLife.Messaging;

/// <summary>The service's one connection to RabbitMQ, opened on first use. Channels are opened from it as needed.</summary>
public sealed class RabbitMqConnection : IAsyncDisposable, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Func<CancellationToken, Task<IConnection>> _open;
    private IConnection? _connection;

    /// <summary>Initializes a new instance of the <see cref="RabbitMqConnection"/> class.</summary>
    /// <param name="options">Where RabbitMQ is.</param>
    public RabbitMqConnection(IOptions<RabbitMqOptions> options)
        : this(cancellationToken => OpenAsync(options.Value, cancellationToken))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RabbitMqConnection"/> class that opens connections with <paramref name="open"/> (unit tests).</summary>
    /// <param name="open">Opens the connection.</param>
    internal RabbitMqConnection(Func<CancellationToken, Task<IConnection>> open)
    {
        _open = open;
    }

    /// <summary>Opens a channel, and the connection first if it isn't open yet.</summary>
    /// <param name="cancellationToken">Cancels the wait for RabbitMQ.</param>
    /// <returns>A new channel. The caller disposes it.</returns>
    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken)
    {
        IConnection connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // A service provider disposed synchronously (e.g. in a test) needs this; the host itself uses DisposeAsync
        _connection?.Dispose();
        _connection = null;
        _lock.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _lock.Dispose();
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _connection ??= await _open(cancellationToken).ConfigureAwait(false);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static Task<IConnection> OpenAsync(RabbitMqOptions settings, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
        };
        return factory.CreateConnectionAsync(cancellationToken);
    }
}
