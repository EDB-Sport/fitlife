using BookingService.Application;

namespace BookingService.IntegrationTests.ClassSessions;

/// <summary>Membership as the tests want it to answer (#260): by default an active member with a new member id.</summary>
internal sealed class FakeMembershipClient : IMembershipClient
{
    /// <summary>Gets or sets what Membership answers for every account.</summary>
    public MemberLookup Answer { get; set; } = MemberLookup.Found(Guid.NewGuid(), isActive: true);

    /// <summary>Gets the account id Membership was last asked about.</summary>
    public Guid? AskedAccountId { get; private set; }

    /// <summary>Gets the token Membership last got.</summary>
    public string? AskedBearerToken { get; private set; }

    /// <inheritdoc />
    public Task<MemberLookup> FindMemberAsync(Guid accountId, string bearerToken, CancellationToken cancellationToken)
    {
        AskedAccountId = accountId;
        AskedBearerToken = bearerToken;
        return Task.FromResult(Answer);
    }
}
