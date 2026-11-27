using System.Globalization;
using MemberPortal.Web.Clients;
using Microsoft.AspNetCore.Components;

namespace MemberPortal.Web.Components.Pages;

/// <summary>
/// The page <c>/hold</c> (#205): the upcoming class sessions day by day, for anyone, without logging in. The filter is a
/// GET form, so the page renders statically. The centres in the filter come from Booking's answer: no endpoint lists them.
/// <c>startTime</c> is shown as Booking sends it, with no time zone conversion (the runtime image has no culture data).
/// </summary>
/// <param name="bookingClient">Booking, through the gateway.</param>
public partial class ClassSchedule(IBookingClient bookingClient)
{
    /// <summary>Shown when Booking answers an error, or nothing.</summary>
    public const string UnavailableSentence = "Vi kan ikke hente holdene lige nu. Prøv igen om lidt.";

    /// <summary>Shown when there is no class session to show.</summary>
    public const string EmptySentence = "Der er ingen hold at vise.";

    /// <summary>Shown instead of the free seats when none is left.</summary>
    public const string FullText = "Fuldt hold";

    /// <summary>Shown instead of the free seats when the class session is cancelled (Hold aflyst).</summary>
    public const string CancelledText = "Aflyst";

    [SupplyParameterFromQuery(Name = "centerId")]
    private Guid? CenterId { get; set; }

    private string? ErrorSentence { get; set; }

    private IReadOnlyList<ScheduleCenter> Centers { get; set; } = [];

    private IReadOnlyList<ScheduleDay> Days { get; set; } = [];

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        IReadOnlyList<ClassSessionResponse> sessions;
        try
        {
            sessions = await bookingClient.GetClassSessionsAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (BookingUnavailableException)
        {
            ErrorSentence = UnavailableSentence;
            return;
        }

        Centers = [.. sessions.DistinctBy(session => session.CenterId).Select(session => new ScheduleCenter(session.CenterId, session.CenterName))];
        Days = [.. sessions
            .Where(session => CenterId is null || session.CenterId == CenterId)
            .GroupBy(session => session.StartTime.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .Select(day => new ScheduleDay(day.Key, [.. day]))];
    }

    private static string Time(ClassSessionResponse session)
    {
        return session.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FreeSeats(ClassSessionResponse session)
    {
        if (session.IsCancelled)
        {
            return CancelledText;
        }

        if (session.FreeSeats <= 0)
        {
            return FullText;
        }

        return session.FreeSeats.ToString(CultureInfo.InvariantCulture);
    }

    private bool IsChosen(ScheduleCenter center)
    {
        return center.Id == CenterId;
    }

    /// <summary>One centre in the filter.</summary>
    private sealed record ScheduleCenter(Guid Id, string Name);

    /// <summary>One day of the schedule, <c>dd-MM-yyyy</c>, with its class sessions in Booking's order.</summary>
    private sealed record ScheduleDay(string Date, IReadOnlyList<ClassSessionResponse> ClassSessions);
}
