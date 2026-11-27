using MembershipService.Domain;

namespace MembershipService.Api;

/// <summary>What the lookups read: members, memberships and the clock for today's status. Bound with <c>[AsParameters]</c>.</summary>
/// <param name="Members">The members.</param>
/// <param name="Memberships">The memberships.</param>
/// <param name="Clock">Today's date, in UTC.</param>
public sealed record MembershipReaders(IMemberRepository Members, IMembershipRepository Memberships, TimeProvider Clock);
