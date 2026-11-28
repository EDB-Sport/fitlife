using System.Collections.Concurrent;
using McpServer.Booking;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Client;

namespace McpServer.IntegrationTests;

/// <summary>One log line per tool call, with the account and the tool, so staff can follow a call in Grafana (#216).</summary>
public sealed class ToolCallLogTests
{
    private const string AccountId = "2f0c5d4e-0000-4000-8000-000000000001";

    [Test]
    public async Task ToolCall_ByAMember_LogsAccountIdAndToolName()
    {
        // Arrange
        using var logs = new CapturingLoggerProvider();
        using var app = new McpFactory(new EmptyBookingClient());
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host
            .ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        using HttpClient http = factory.CreateClient();
        string token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Claims = new Dictionary<string, object>(StringComparer.Ordinal) { ["sub"] = AccountId } });
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Add("X-Correlation-ID", "mcp-call-log-test");
        await using HttpClientTransport transport = McpFactory.TransportFor(http);
        await using McpClient client = await McpClient.CreateAsync(transport).ConfigureAwait(false);

        // Act
        await client.CallToolAsync("list_class_sessions").ConfigureAwait(false);

        // Assert
        LogLine line = logs.Lines.Single(logged => logged.Values.ContainsKey("AccountId"));
        Assert.That(line.Level, Is.EqualTo(LogLevel.Information));
        Assert.That(line.Values["AccountId"], Is.EqualTo(AccountId));
        Assert.That(line.Values["ToolName"], Is.EqualTo("list_class_sessions"));
        Assert.That(line.CorrelationId, Is.EqualTo("mcp-call-log-test"));
        Assert.That(line.Message, Does.Not.Contain(token));
    }

    [Test]
    public async Task ToolCall_WithoutAToken_LogsNoAccount()
    {
        // Arrange
        using var logs = new CapturingLoggerProvider();
        using var app = new McpFactory(new EmptyBookingClient());
        using WebApplicationFactory<Program> factory = app.WithWebHostBuilder(host => host
            .ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        using HttpClient http = factory.CreateClient();
        await using HttpClientTransport transport = McpFactory.TransportFor(http);
        await using McpClient client = await McpClient.CreateAsync(transport).ConfigureAwait(false);

        // Act
        await client.CallToolAsync("list_class_sessions").ConfigureAwait(false);

        // Assert
        LogLine line = logs.Lines.Single(logged => logged.Values.ContainsKey("AccountId"));
        Assert.That(line.Values["AccountId"], Is.EqualTo("none"));
    }

    private sealed record LogLine(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Values, string? CorrelationId);

    private sealed class EmptyBookingClient : IBookingClient
    {
        public Task<IReadOnlyList<ClassSession>> GetClassSessionsAsync(Guid? centerId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ClassSession>>([]);
        }
    }

    // Records each log line with its values and the CorrelationId of the scope it is written in
    private sealed class CapturingLoggerProvider : ILoggerProvider, ILogger, ISupportExternalScope
    {
        private readonly ConcurrentQueue<LogLine> _lines = new();
        private IExternalScopeProvider? _scopes;

        public IEnumerable<LogLine> Lines => _lines;

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopes = scopeProvider;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return _scopes?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Dictionary<string, object?> values = new(StringComparer.Ordinal);
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (KeyValuePair<string, object?> pair in pairs)
                {
                    values[pair.Key] = pair.Value;
                }
            }

            List<string> correlationIds = [];
            _scopes?.ForEachScope(AddCorrelationId, correlationIds);
            string? correlationId = correlationIds.LastOrDefault();
            _lines.Enqueue(new LogLine(logLevel, formatter(state, exception), values, correlationId));
        }

        private static void AddCorrelationId(object? scope, List<string> ids)
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> scoped)
            {
                ids.AddRange(scoped.Where(value => string.Equals(value.Key, "CorrelationId", StringComparison.Ordinal)).Select(value => value.Value).OfType<string>());
            }
        }

        public void Dispose()
        {
            // Nothing to release
        }
    }
}
