using McpServer.Booking;
using McpServer.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using Moq;

namespace McpServer.Tests.Tools;

/// <summary>
/// The MCP tool list_class_sessions (#214): it asks Booking through <see cref="IBookingClient"/> and passes the answer on.
/// The server holds no business rule (PA3), so it doesn't filter, sort or decide anything.
/// </summary>
public sealed class ClassScheduleToolsTests
{
    private static readonly Guid CenterId = Guid.Parse("6f1f6d0e-0b0e-4a6e-9a43-3c6a0d6f0001");

    private static readonly ClassSession LaterSession = new(
        Guid.Parse("6f1f6d0e-0b0e-4a6e-9a43-3c6a0d6f0010"),
        CenterId,
        "Aarhus",
        Guid.Parse("6f1f6d0e-0b0e-4a6e-9a43-3c6a0d6f0020"),
        "Yoga",
        "Louise",
        new DateTimeOffset(2026, 12, 2, 18, 0, 0, TimeSpan.FromHours(1)),
        DurationMinutes: 60,
        Capacity: 15,
        FreeSeats: 0,
        IsCancelled: true);

    private static readonly ClassSession EarlierSession = LaterSession with
    {
        Id = Guid.Parse("6f1f6d0e-0b0e-4a6e-9a43-3c6a0d6f0011"),
        ClassTypeName = "Spinning",
        StartTime = new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.FromHours(1)),
        FreeSeats = 3,
        IsCancelled = false,
    };

    [Test]
    public async Task ListClassSessions_WhenBookingAnswers_ReturnsItsClassSessions()
    {
        // Arrange: in Booking's order, which isn't by start and has a cancelled session; the tool keeps it all as it is
        ClassSession[] fromBooking = [LaterSession, EarlierSession];
        var booking = new Mock<IBookingClient>();
        booking.Setup(client => client.GetClassSessionsAsync(CenterId, It.IsAny<CancellationToken>())).ReturnsAsync(fromBooking);
        var tools = new ClassScheduleTools(booking.Object, NullLogger<ClassScheduleTools>.Instance);

        // Act
        IReadOnlyList<ClassSession> result = await tools.ListClassSessions(CenterId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result, Is.EqualTo(fromBooking));
    }

    [Test]
    public async Task ListClassSessions_WithoutACenterId_AsksBookingForEveryCenter()
    {
        // Arrange
        var booking = new Mock<IBookingClient>();
        booking.Setup(client => client.GetClassSessionsAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync([EarlierSession]);
        var tools = new ClassScheduleTools(booking.Object, NullLogger<ClassScheduleTools>.Instance);

        // Act
        IReadOnlyList<ClassSession> result = await tools.ListClassSessions(centerId: null, CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.That(result, Is.EqualTo(new[] { EarlierSession }));
    }

    [Test]
    public void ListClassSessions_WhenBookingCantBeReached_ReturnsAnErrorWithoutAStackTrace()
    {
        // Arrange: the inner exception carries details that must not reach the AI assistant
        const string Detail = "connection refused by gateway.internal:80";
        var booking = new Mock<IBookingClient>();
        booking.Setup(client => client.GetClassSessionsAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookingUnavailableException("Booking can't be reached.", new HttpRequestException(Detail)));
        var tools = new ClassScheduleTools(booking.Object, NullLogger<ClassScheduleTools>.Instance);

        // Act
        McpException? error = Assert.ThrowsAsync<McpException>(() => tools.ListClassSessions(centerId: null, CancellationToken.None));

        // Assert: the SDK turns an McpException into a tool error with exactly its message
        Assert.That(error?.Message, Is.EqualTo("The class schedule can't be fetched right now. Try again later."));
        Assert.That(error?.Message, Does.Not.Contain(Detail).And.Not.Contain(" at "));
    }
}
