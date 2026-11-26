using System.Text.Json;
using McpServer.Booking;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpServer.IntegrationTests;

/// <summary>The tool list_class_sessions, called through a real MCP client over HTTP (#214).</summary>
public sealed class ListClassSessionsToolTests
{
    private static readonly Guid CenterId = Guid.Parse("6f1f6d0e-0b0e-4a6e-9a43-3c6a0d6f0001");

    [Test]
    public async Task CallTool_WhenBookingAnswers_ReturnsTheFieldsOfBookingsContract()
    {
        // Arrange
        var session = new ClassSession(
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
        using var factory = new McpFactory(new StubBookingClient(_ => [session]));
        using HttpClient http = factory.CreateClient();
        await using McpClient client = await McpFactory.ConnectAsync(http).ConfigureAwait(false);

        // Act
        CallToolResult result = await client.CallToolAsync("list_class_sessions", new Dictionary<string, object?> { ["centerId"] = CenterId }).ConfigureAwait(false);

        // Assert
        Assert.That(result.IsError, Is.Not.True);
        using var body = JsonDocument.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
        JsonElement listed = body.RootElement.EnumerateArray().Single();
        Assert.That(listed.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(FieldsOfBookingsContract()));
        Assert.That(listed.GetProperty("id").GetGuid(), Is.EqualTo(session.Id));
        Assert.That(listed.GetProperty("freeSeats").GetInt32(), Is.EqualTo(3));
    }

    [Test]
    public async Task CallTool_WhenBookingCantBeReached_IsAToolErrorWithOneSentence()
    {
        // Arrange
        using var factory = new McpFactory(new StubBookingClient(_ => throw new BookingUnavailableException("Booking can't be reached.", new HttpRequestException("connection refused by gateway.internal:80"))));
        using HttpClient http = factory.CreateClient();
        await using McpClient client = await McpFactory.ConnectAsync(http).ConfigureAwait(false);

        // Act
        CallToolResult result = await client.CallToolAsync("list_class_sessions").ConfigureAwait(false);

        // Assert
        Assert.That(result.IsError, Is.True);
        Assert.That(result.Content.OfType<TextContentBlock>().Single().Text, Is.EqualTo("The class schedule can't be fetched right now. Try again later."));
    }

    /// <summary>The property names of <c>ClassSessionResponse</c> in Booking's committed contract.</summary>
    private static IEnumerable<string> FieldsOfBookingsContract()
    {
        string path = Path.Combine(RepoRoot(), "contracts", "openapi", "bookingservice.json");
        using var contract = JsonDocument.Parse(File.ReadAllText(path));
        return [.. contract.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ClassSessionResponse").GetProperty("properties").EnumerateObject().Select(property => property.Name)];
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FitLife.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No FitLife.slnx above " + AppContext.BaseDirectory);
    }

    private sealed class StubBookingClient(Func<Guid?, IReadOnlyList<ClassSession>> answer) : IBookingClient
    {
        public Task<IReadOnlyList<ClassSession>> GetClassSessionsAsync(Guid? centerId, CancellationToken cancellationToken)
        {
            return Task.FromResult(answer(centerId));
        }
    }
}
