using MemberPortal.Web.Clients;

namespace MemberPortal.Web.Tests;

/// <summary>Booking in front of the portal's pages: answers a fixed class schedule or throws, and counts the calls.</summary>
internal sealed class FakeBookingClient : IBookingClient
{
    public IReadOnlyList<ClassSessionResponse> ClassSessions { get; set; } = [];

    public Exception? Failure { get; set; }

    public int Calls { get; private set; }

    public Task<IReadOnlyList<ClassSessionResponse>> GetClassSessionsAsync(CancellationToken cancellationToken)
    {
        Calls++;
        if (Failure is not null)
        {
            return Task.FromException<IReadOnlyList<ClassSessionResponse>>(Failure);
        }

        return Task.FromResult(ClassSessions);
    }
}
