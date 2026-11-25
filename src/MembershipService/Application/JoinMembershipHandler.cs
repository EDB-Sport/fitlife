using MembershipService.Domain;

namespace MembershipService.Application;

/// <summary>Joins an account to FitLife: one member per account, with an active membership.</summary>
/// <param name="members">The members.</param>
/// <param name="memberships">The memberships.</param>
/// <param name="clock">Today's date.</param>
public sealed class JoinMembershipHandler(IMemberRepository members, IMembershipRepository memberships, TimeProvider clock)
{
    /// <summary>Joins the account, unless it is already a member.</summary>
    /// <param name="command">The account that joins.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new member and membership, or that the account was already a member.</returns>
    public async Task<JoinResult> HandleAsync(JoinMembershipCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await members.FindByAccountIdAsync(command.AccountId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return JoinResult.AlreadyAMember();
        }

        var member = Member.Join(command.AccountId);
        var membership = Membership.Start(member.Id, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));

        // The membership first: a lookup goes through the member, so a membership without one is never seen
        await memberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);
        if (!await members.TryAddAsync(member, cancellationToken).ConfigureAwait(false))
        {
            // Another join for the same account got there first
            await memberships.RemoveAsync(membership.Id, cancellationToken).ConfigureAwait(false);
            return JoinResult.AlreadyAMember();
        }

        return JoinResult.Success(member, membership);
    }
}
