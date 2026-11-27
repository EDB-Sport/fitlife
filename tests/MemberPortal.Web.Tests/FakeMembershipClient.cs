using MemberPortal.Web.Clients;

namespace MemberPortal.Web.Tests;

/// <summary>Membership in front of the portal's pages: answers one <see cref="JoinMembershipAnswer"/> or throws, and counts the calls.</summary>
internal sealed class FakeMembershipClient : IMembershipClient
{
    public JoinMembershipAnswer Answer { get; set; } = JoinMembershipAnswer.Joined;

    public Exception? Failure { get; set; }

    public int Calls { get; private set; }

    public string? AccessToken { get; private set; }

    public Task<JoinMembershipAnswer> JoinAsync(string? accessToken, CancellationToken cancellationToken)
    {
        Calls++;
        AccessToken = accessToken;
        if (Failure is not null)
        {
            return Task.FromException<JoinMembershipAnswer>(Failure);
        }

        return Task.FromResult(Answer);
    }
}
