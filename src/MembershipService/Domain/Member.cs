namespace MembershipService.Domain;

/// <summary>
/// Medlem: a person with a membership at FitLife. Keeps the id of the <c>Account</c> in Identity that joined,
/// so other services can find the member from the token (CONTEXT-MAP section 3).
/// </summary>
public sealed class Member
{
    private Member(Guid id, string accountId)
    {
        Id = id;
        AccountId = accountId;
    }

    /// <summary>The member id other contexts refer to.</summary>
    public Guid Id { get; }

    /// <summary>The id of the account in Identity.</summary>
    public string AccountId { get; }

    /// <summary>A person with an account joins FitLife.</summary>
    /// <param name="accountId">The account id from the token.</param>
    /// <returns>The new member.</returns>
    public static Member Join(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        return new Member(Guid.NewGuid(), accountId);
    }

    /// <summary>Rebuilds a stored member.</summary>
    /// <param name="id">The member id.</param>
    /// <param name="accountId">The account id.</param>
    /// <returns>The member.</returns>
    public static Member Restore(Guid id, string accountId)
    {
        return new Member(id, accountId);
    }
}
