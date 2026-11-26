using System.Net;
using System.Text;
using FitLife.Correlation;
using McpServer.Booking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpServer.Tests.Booking;

/// <summary>
/// The server asks Booking for the class schedule through the gateway (#214). The real registration, with a stub handler in
/// place of the gateway.
/// </summary>
public sealed class BookingClientTests
{
    private const string Schedule = """
        [{"id":"6f1f6d0e-0b0e-4a6e-9a43-3c6a0d6f0010","centerId":"6f1f6d0e-0b0e-4a6e-9a43-3c6a0d6f0001","centerName":"Aarhus",
          "classTypeId":"6f1f6d0e-0b0e-4a6e-9a43-3c6a0d6f0020","classTypeName":"Spinning","instructorName":"Louise",
          "startTime":"2026-12-01T17:00:00+01:00","durationMinutes":45,"capacity":20,"freeSeats":3,"isCancelled":false}]
        """;

    private static readonly Guid CenterId = Guid.Parse("6f1f6d0e-0b0e-4a6e-9a43-3c6a0d6f0001");

    [Test]
    public async Task GetClassSessionsAsync_WithACenterId_AsksBookingThroughTheGateway()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, "[]");
        using ServiceProvider services = Services(gateway);

        // Act
        await Get(services, CenterId).ConfigureAwait(false);

        // Assert
        Assert.That(gateway.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(gateway.Uri, Is.EqualTo(new Uri($"https://gateway.test/api/booking/class-sessions?centerId={CenterId}")));
    }

    [Test]
    public async Task GetClassSessionsAsync_WithoutACenterId_AsksForEveryCenter()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, "[]");
        using ServiceProvider services = Services(gateway);

        // Act
        await Get(services, centerId: null).ConfigureAwait(false);

        // Assert
        Assert.That(gateway.Uri, Is.EqualTo(new Uri("https://gateway.test/api/booking/class-sessions")));
    }

    [Test]
    public async Task GetClassSessionsAsync_SendsTheCorrelationId()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, "[]");
        using ServiceProvider services = Services(gateway);

        // Act
        using (CorrelationContext.Begin(NullLogger.Instance, "correlation-1"))
        {
            await Get(services, centerId: null).ConfigureAwait(false);
        }

        // Assert
        Assert.That(gateway.CorrelationId, Is.EqualTo("correlation-1"));
    }

    [Test]
    public async Task GetClassSessionsAsync_WhenBookingAnswers_ReturnsTheClassSessionsWithTheFieldsOfBookingsContract()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, Schedule);
        using ServiceProvider services = Services(gateway);

        // Act
        IReadOnlyList<ClassSession> sessions = await Get(services, centerId: null).ConfigureAwait(false);

        // Assert
        var expected = new ClassSession(
            Guid.Parse("6f1f6d0e-0b0e-4a6e-9a43-3c6a0d6f0010"),
            CenterId,
            "Aarhus",
            Guid.Parse("6f1f6d0e-0b0e-4a6e-9a43-3c6a0d6f0020"),
            "Spinning",
            "Louise",
            new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.FromHours(1)),
            DurationMinutes: 45,
            Capacity: 20,
            FreeSeats: 3,
            IsCancelled: false);
        Assert.That(sessions, Is.EqualTo(new[] { expected }));
    }

    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.NotFound)]
    public async Task GetClassSessionsAsync_WhenBookingAnswersAnError_ThrowsBookingUnavailable(HttpStatusCode status)
    {
        // Arrange
        using var gateway = new StubGateway(status, "{}");
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => Get(services, centerId: null), Throws.TypeOf<BookingUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task GetClassSessionsAsync_WhenBookingCantBeReached_ThrowsBookingUnavailable()
    {
        // Arrange
        using var gateway = StubGateway.ThatCantBeReached();
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => Get(services, centerId: null), Throws.TypeOf<BookingUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task GetClassSessionsAsync_WhenBookingDoesntAnswer_ThrowsBookingUnavailable()
    {
        // Arrange: HttpClient gives up with a TaskCanceledException (inner TimeoutException) when nothing answers in time
        using var gateway = StubGateway.ThatDoesntAnswer();
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => Get(services, centerId: null), Throws.TypeOf<BookingUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task GetClassSessionsAsync_WhenTheConnectionBreaksWhileReadingTheAnswer_ThrowsBookingUnavailable()
    {
        // Arrange: Booking answered 200, then the connection ended in the middle of the body. .NET throws an HttpIOException (an IOException)
        using var gateway = StubGateway.ThatBreaksWhileReadingTheBody(new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely."));
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => Get(services, centerId: null), Throws.TypeOf<BookingUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task GetClassSessionsAsync_WhenTheTimeoutHitsWhileReadingTheAnswer_ThrowsBookingUnavailable()
    {
        // Arrange: the timeout fires while the body is read, and the read stream throws a plain OperationCanceledException, not a TaskCanceledException
        using var gateway = StubGateway.ThatBreaksWhileReadingTheBody(new OperationCanceledException("The read was cancelled."));
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => Get(services, centerId: null), Throws.TypeOf<BookingUnavailableException>()).ConfigureAwait(false);
    }

    [Test]
    public async Task GetClassSessionsAsync_WhenTheCallerCancels_StaysACancellation()
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, "[]");
        using ServiceProvider services = Services(gateway);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(false);

        // Act & Assert: the caller's own cancellation is not Booking's fault
        await Assert.ThatAsync(
            () => services.GetRequiredService<IBookingClient>().GetClassSessionsAsync(centerId: null, cancelled.Token),
            Throws.InstanceOf<OperationCanceledException>()).ConfigureAwait(false);
    }

    [TestCase("not json")]
    [TestCase("null")]
    public async Task GetClassSessionsAsync_WhenTheAnswerCantBeRead_ThrowsBookingUnavailable(string body)
    {
        // Arrange
        using var gateway = new StubGateway(HttpStatusCode.OK, body);
        using ServiceProvider services = Services(gateway);

        // Act & Assert
        await Assert.ThatAsync(() => Get(services, centerId: null), Throws.TypeOf<BookingUnavailableException>()).ConfigureAwait(false);
    }

    private static Task<IReadOnlyList<ClassSession>> Get(ServiceProvider services, Guid? centerId)
    {
        return services.GetRequiredService<IBookingClient>().GetClassSessionsAsync(centerId, CancellationToken.None);
    }

    private static ServiceProvider Services(StubGateway gateway)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [BookingClient.BaseUrlConfigurationKey] = "https://gateway.test" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddBookingClient();
        services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => gateway));
        return services.BuildServiceProvider();
    }

    /// <summary>The gateway in front of Booking: answers with one status and body, and remembers the request.</summary>
    private sealed class StubGateway : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly Exception? _failure;
        private readonly Exception? _failureWhileReadingTheBody;

        public StubGateway(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        private StubGateway(Exception failure, bool whileReadingTheBody)
        {
            _status = HttpStatusCode.OK;
            _body = string.Empty;
            if (whileReadingTheBody)
            {
                _failureWhileReadingTheBody = failure;
            }
            else
            {
                _failure = failure;
            }
        }

        public HttpMethod? Method { get; private set; }

        public Uri? Uri { get; private set; }

        public string? CorrelationId { get; private set; }

        public static StubGateway ThatCantBeReached()
        {
            return new StubGateway(new HttpRequestException("No connection could be made."), whileReadingTheBody: false);
        }

        public static StubGateway ThatDoesntAnswer()
        {
            return new StubGateway(new TaskCanceledException("The request timed out.", new TimeoutException()), whileReadingTheBody: false);
        }

        /// <summary>Answers 200 with a body whose stream throws the failure when it is read.</summary>
        public static StubGateway ThatBreaksWhileReadingTheBody(Exception failure)
        {
            return new StubGateway(failure, whileReadingTheBody: true);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
            CorrelationId = null;
            if (request.Headers.TryGetValues(CorrelationContext.HeaderName, out IEnumerable<string>? values))
            {
                CorrelationId = values.Single();
            }

            if (_failure is not null)
            {
                throw _failure;
            }

            if (_failureWhileReadingTheBody is not null)
            {
                var broken = new StreamContent(new FailingStream(_failureWhileReadingTheBody));
                broken.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(new HttpResponseMessage(_status) { Content = broken });
            }

            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
        }
    }

    /// <summary>A stream whose every read throws, as a connection that ends or times out in the middle of the body.</summary>
    private sealed class FailingStream(Exception failure) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw failure;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw failure;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
