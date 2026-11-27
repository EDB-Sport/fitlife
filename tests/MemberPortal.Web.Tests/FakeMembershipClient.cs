using MemberPortal.Web.Clients;

namespace MemberPortal.Web.Tests;

/// <summary>Membership in front of the portal's pages: answers one <see cref="JoinMembershipAnswer"/> and one membership, or throws, and counts the calls.</summary>
internal sealed class FakeMembershipClient : IMembershipClient
{
    public JoinMembershipAnswer Answer { get; set; } = JoinMembershipAnswer.Joined;

    public MyMembershipResponse? MyMembership { get; set; }

    public PaymentAnswer PaymentAnswer { get; set; } = new(Membership: null, RejectionCode: null);

    public int PayCalls { get; private set; }

    public Exception? Failure { get; set; }

    public Exception? PaymentFailure { get; set; }

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

    public Task<MyMembershipResponse?> GetMyMembershipAsync(CancellationToken cancellationToken)
    {
        Calls++;
        if (Failure is not null)
        {
            return Task.FromException<MyMembershipResponse?>(Failure);
        }

        return Task.FromResult(MyMembership);
    }

    public Task<PaymentAnswer> PayAsync(CancellationToken cancellationToken)
    {
        PayCalls++;
        if (PaymentFailure is not null)
        {
            return Task.FromException<PaymentAnswer>(PaymentFailure);
        }

        return Task.FromResult(PaymentAnswer);
    }
}
