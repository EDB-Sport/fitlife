using System.Collections.Concurrent;
using FitLife.Messaging;
using FitLife.Messaging.Events;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationService.Models;
using NotificationService.Repositories;

namespace NotificationService.IntegrationTests;

/// <summary>
/// ReservationCancelled, ClassSessionCancelled and ClassSessionChanged on the real RabbitMQ each become a notification,
/// stored in the real MongoDB (#222).
/// </summary>
public sealed class BookingEventsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [TestCase("ReservationCancelled")]
    [TestCase("ClassSessionCancelled")]
    [TestCase("ClassSessionChanged")]
    public async Task Consume_EachBookingEventExample_StoresANotification(string kind)
    {
        // Arrange
        using var logs = new WaitingLoggerProvider();
        await using var factory = new NotificationServiceFactory(logs);
        factory.CreateClient().Dispose();

        // Act
        string memberId = kind switch
        {
            nameof(ReservationCancelled) => await PublishExampleAsync<ReservationCancelled>(factory, example => example.MemberId).ConfigureAwait(false),
            nameof(ClassSessionCancelled) => await PublishExampleAsync<ClassSessionCancelled>(factory, example => example.MemberIds[0]).ConfigureAwait(false),
            _ => await PublishExampleAsync<ClassSessionChanged>(factory, example => example.MemberIds[0]).ConfigureAwait(false),
        };

        // Assert: the line is written once the notification is stored
        await logs.WaitForAsync(memberId, Timeout).ConfigureAwait(false);
        IReadOnlyList<Notification> stored = await factory.Services.GetRequiredService<INotificationRepository>()
            .ListForMemberAsync(memberId, CancellationToken.None).ConfigureAwait(false);
        Assert.That(stored.Select(notification => notification.Kind), Has.One.EqualTo(kind));
    }

    private static async Task<string> PublishExampleAsync<TEvent>(WebApplicationFactory<Program> factory, Func<TEvent, string> memberOf)
        where TEvent : IIntegrationEvent
    {
        await factory.Services.GetRequiredService<RabbitMqEventConsumer<TEvent>>().Consuming.WaitAsync(Timeout).ConfigureAwait(false);
        var example = (TEvent)TEvent.Example;
        await new RabbitMqEventPublisher(factory.Services.GetRequiredService<RabbitMqConnection>())
            .PublishAsync(example, CancellationToken.None).ConfigureAwait(false);
        return memberOf(example);
    }

    /// <summary>Captures every log line, and lets a test wait for the first notification line for a member.</summary>
    private sealed class WaitingLoggerProvider : ILoggerProvider, ILogger
    {
        private readonly ConcurrentQueue<string> _lines = new();
        private readonly SemaphoreSlim _logged = new(0);

        public async Task WaitForAsync(string memberId, TimeSpan timeout)
        {
            using var deadline = new CancellationTokenSource(timeout);
            string expected = $"Notification to member {memberId}:";
            while (!_lines.Any(line => line.StartsWith(expected, StringComparison.Ordinal)))
            {
                await _logged.WaitAsync(deadline.Token).ConfigureAwait(false);
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _lines.Enqueue(formatter(state, exception));
            _logged.Release();
        }

        public void Dispose()
        {
            _logged.Dispose();
        }
    }
}
