using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FitLife.Messaging;

/// <summary>The service's one connection to RabbitMQ, opened on first use. Channels are opened from it as needed.</summary>
/// <param name="options">Where RabbitMQ is.</param>
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    /// <summary>Opens a channel, and the connection first if it isn't open yet.</summary>
    /// <param name="cancellationToken">Cancels the wait for RabbitMQ.</param>
    /// <returns>A new channel. The caller disposes it.</returns>
    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken)
    {
        IConnection connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        _lock.Dispose();
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is null)
            {
                RabbitMqOptions settings = options.Value;
                var factory = new ConnectionFactory
                {
                    HostName = settings.HostName,
                    Port = settings.Port,
                    UserName = settings.UserName,
                    Password = settings.Password,
                };
                _connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            }

            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }
}
