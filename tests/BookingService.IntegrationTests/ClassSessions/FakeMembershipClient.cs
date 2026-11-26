using BookingService.Application;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>Membership as the tests want it to answer (#260): by default an active member with a new member id.</summary>
internal sealed class FakeMembershipClient : IMembershipClient
{
    /// <summary>Gets or sets what Membership answers for every account.</summary>
    public MemberLookup Answer { get; set; } = MemberLookup.Found(Guid.NewGuid(), isActive: true);

    /// <inheritdoc />
    public Task<MemberLookup> FindMemberAsync(Guid accountId, string bearerToken, CancellationToken cancellationToken)
    {
        return Task.FromResult(Answer);
    }
}
