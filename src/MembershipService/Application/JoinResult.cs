using MembershipService.Domain;

namespace MembershipService.Application;

/// <summary>What happened when an account tried to join.</summary>
public sealed class JoinResult
{
    private JoinResult(Member? member, Membership? membership)
    {
        Member = member;
        Membership = membership;
    }

    /// <summary>The new member, or null when the account was already a member.</summary>
    public Member? Member { get; }

    /// <summary>The new membership, or null when the account was already a member.</summary>
    public Membership? Membership { get; }

    /// <summary>Whether the account became a member.</summary>
    public bool Joined => Member is not null;

    /// <summary>The account became a member.</summary>
    /// <param name="member">The new member.</param>
    /// <param name="membership">The new membership.</param>
    /// <returns>The result.</returns>
    public static JoinResult Success(Member member, Membership membership)
    {
        return new JoinResult(member, membership);
    }

    /// <summary>The account was already a member.</summary>
    /// <returns>The result.</returns>
    public static JoinResult AlreadyAMember()
    {
        return new JoinResult(member: null, membership: null);
    }
}
