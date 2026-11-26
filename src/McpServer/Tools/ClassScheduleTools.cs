using System.ComponentModel;
using System.Text.Json;
using McpServer.Booking;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpServer.Tools;

/// <summary>
/// The class schedule as an MCP tool (#214). It changes nothing: it asks Booking through <see cref="IBookingClient"/> and passes the
/// answer on, with no filter, sort or rule of its own (PA3: the server holds no business rule). It needs no token: the schedule can be
/// seen without logging in.
/// </summary>
/// <param name="booking">Asks Booking for the class schedule.</param>
/// <param name="logger">Logs why Booking couldn't answer.</param>
[McpServerToolType]
public sealed class ClassScheduleTools(IBookingClient booking, ILogger<ClassScheduleTools> logger)
{
    /// <summary>Lists the upcoming class sessions of a centre, or of every centre, as Booking answers them.</summary>
    /// <param name="centerId">The centre; leave it out for every centre.</param>
    /// <param name="cancellationToken">Stops the call.</param>
    /// <returns>
    /// The class sessions as a JSON array, with the fields of Booking's contract. When Booking answers an error or doesn't answer, a tool
    /// error with one sentence: never an exception's text or stack trace.
    /// </returns>
    [McpServerTool(Name = "list_class_sessions")]
    [Description("Lists the upcoming class sessions of a centre, or of every centre, with names and free seats. A cancelled class session has no free seats. Needs no login.")]
    public async Task<CallToolResult> ListClassSessions(
        [Description("The centre's id; leave it out for every centre.")] Guid? centerId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<ClassSession> sessions = await booking.GetClassSessionsAsync(centerId, cancellationToken).ConfigureAwait(false);
            return new CallToolResult { Content = [new TextContentBlock { Text = JsonSerializer.Serialize(sessions, JsonSerializerOptions.Web) }] };
        }
        catch (BookingUnavailableException exception)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(exception, "Booking couldn't give the class schedule");
            }

            // Our own sentence as the tool error: the SDK would put its own words before an exception's message
            return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = "The class schedule can't be fetched right now. Try again later." }] };
        }
    }
}
