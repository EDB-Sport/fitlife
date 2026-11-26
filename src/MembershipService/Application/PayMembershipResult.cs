namespace MembershipService.Application;

/// <summary>What happened when a member paid the missing fee.</summary>
public enum PayMembershipResult
{
    /// <summary>The charge was accepted and the membership is Aktiv.</summary>
    Paid = 0,

    /// <summary>The account hasn't joined FitLife.</summary>
    NotAMember = 1,

    /// <summary>The membership doesn't have Betaling mangler, so nothing was charged.</summary>
    NothingIsOwed = 2,

    /// <summary>The charge didn't go through (declined, a provider error or no answer), so nothing was saved.</summary>
    ChargeDeclined = 3,
}
