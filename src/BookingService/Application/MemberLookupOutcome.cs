namespace BookingService.Application;

/// <summary>Which answer <see cref="IMembershipClient.FindMemberAsync"/> gave.</summary>
public enum MemberLookupOutcome
{
    /// <summary>The account is a member; the member id and whether the membership is active are known.</summary>
    Found,

    /// <summary>The account is not a member.</summary>
    NotAMember,

    /// <summary>Membership gave no usable answer, so Booking can't tell.</summary>
    Unavailable,
}
