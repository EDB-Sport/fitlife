using System.Net.Http.Json;
using System.Text.Json;

namespace McpServer.Booking;

/// <summary>
/// Booking's class schedule (<c>GET class-sessions</c>), reached through the gateway's route <c>api/booking</c> (#213), over a typed client.
/// Every failure becomes a <see cref="BookingUnavailableException"/>.
/// </summary>
/// <param name="http">A client with the gateway's base address, from <c>IHttpClientFactory</c>.</param>
public sealed class BookingClient(HttpClient http) : IBookingClient
{
    /// <summary>The configuration key of the gateway's address.</summary>
    public const string BaseUrlConfigurationKey = "Gateway:BaseUrl";

    private const string ClassSessionsPath = "api/booking/class-sessions";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClassSession>> GetClassSessionsAsync(Guid? centerId, CancellationToken cancellationToken)
    {
        string path = ClassSessionsPath;
        if (centerId is Guid center)
        {
            path = $"{ClassSessionsPath}?centerId={center}";
        }

        ClassSession[]? sessions;
        try
        {
            sessions = await http.GetFromJsonAsync<ClassSession[]>(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            // No connection, or an error status (GetFromJsonAsync checks it)
            throw new BookingUnavailableException("Booking can't be reached, or answered an error.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Booking didn't answer in time: HttpClient's timeout, as a TaskCanceledException while it waits for the answer
            // and as a plain OperationCanceledException while the body is read. The caller's own cancellation passes on untouched
            throw new BookingUnavailableException("Booking didn't answer in time.", exception);
        }
        catch (IOException exception)
        {
            // The connection ended in the middle of the body (HttpIOException is an IOException)
            throw new BookingUnavailableException("The connection to Booking ended before the answer was read.", exception);
        }
        catch (JsonException exception)
        {
            throw new BookingUnavailableException("Booking's answer can't be read.", exception);
        }

        if (sessions is null)
        {
            throw new BookingUnavailableException("Booking's answer is empty.");
        }

        return sessions;
    }
}
