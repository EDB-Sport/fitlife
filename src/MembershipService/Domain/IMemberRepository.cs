namespace MembershipService.Domain;

/// <summary>Stores members. An account id belongs to at most one member.</summary>
public interface IMemberRepository
{
    /// <summary>Finds the member who joined with the account.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The member, or null.</returns>
    Task<Member?> FindByAccountIdAsync(string accountId, CancellationToken cancellationToken);

    /// <summary>Stores a new member, unless the account is already a member.</summary>
    /// <param name="member">The new member.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>False when the account is already a member.</returns>
    Task<bool> TryAddAsync(Member member, CancellationToken cancellationToken);
}
