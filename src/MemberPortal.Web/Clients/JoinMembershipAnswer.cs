namespace MemberPortal.Web.Clients;

/// <summary>What Membership answers when the account in the token joins (Indmeldelse).</summary>
public enum JoinMembershipAnswer
{
    /// <summary>201: the account is now a member with a membership.</summary>
    Joined,

    /// <summary>201 with <c>membershipStatus</c> <c>PaymentMissing</c>: a member, but the first fee isn't paid, so booking is refused.</summary>
    PaymentMissing,

    /// <summary>409: the account is already a member.</summary>
    AlreadyAMember,
}
