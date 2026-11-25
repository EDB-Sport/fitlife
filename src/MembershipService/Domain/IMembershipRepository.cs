namespace MembershipService.Domain;

/// <summary>Stores memberships.</summary>
public interface IMembershipRepository
{
    /// <summary>Stores a new membership.</summary>
    /// <param name="membership">The membership.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    Task AddAsync(Membership membership, CancellationToken cancellationToken);

    /// <summary>Removes a membership whose member could not be stored.</summary>
    /// <param name="membershipId">The membership id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(Guid membershipId, CancellationToken cancellationToken);

    /// <summary>Finds the member's membership.</summary>
    /// <param name="memberId">The member id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The membership, or null.</returns>
    Task<Membership?> FindByMemberIdAsync(Guid memberId, CancellationToken cancellationToken);
}
