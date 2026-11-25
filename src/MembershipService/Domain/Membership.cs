namespace MembershipService.Domain;

/// <summary>Medlemskab: a member's agreement with FitLife. Refers to its <see cref="Member"/> by id only.</summary>
public sealed class Membership
{
    private Membership(Guid id, Guid memberId, MembershipType type, MembershipStatus status, DateOnly startDate)
    {
        Id = id;
        MemberId = memberId;
        Type = type;
        Status = status;
        StartDate = startDate;
    }

    /// <summary>The membership id.</summary>
    public Guid Id { get; }

    /// <summary>The member the membership belongs to.</summary>
    public Guid MemberId { get; }

    /// <summary>The kind of membership.</summary>
    public MembershipType Type { get; }

    /// <summary>Where the membership is in its life.</summary>
    public MembershipStatus Status { get; private set; }

    /// <summary>The day the membership started.</summary>
    public DateOnly StartDate { get; }

    /// <summary>Whether the membership gives access and the right to book.</summary>
    public bool IsActive => Status == MembershipStatus.Active;

    /// <summary>
    /// A new membership starts the day the member joins, with Betaling mangler until the first fee is paid
    /// (<see cref="MarkPaid"/>), so an interrupted join never gives access without a paid fee (TO-BE-kontekst §8, UC5).
    /// </summary>
    /// <param name="memberId">The member who joined.</param>
    /// <param name="startDate">Today.</param>
    /// <returns>The new membership.</returns>
    public static Membership Start(Guid memberId, DateOnly startDate)
    {
        return new Membership(Guid.NewGuid(), memberId, MembershipType.Standard, MembershipStatus.PaymentMissing, startDate);
    }

    /// <summary>The membership fee was paid: the membership gives access and the right to book.</summary>
    public void MarkPaid()
    {
        Status = MembershipStatus.Active;
    }

    /// <summary>The membership fee was not paid: the membership no longer gives access or the right to book.</summary>
    public void MarkPaymentMissing()
    {
        Status = MembershipStatus.PaymentMissing;
    }

    /// <summary>Rebuilds a stored membership.</summary>
    /// <param name="id">The membership id.</param>
    /// <param name="memberId">The member id.</param>
    /// <param name="type">The kind of membership.</param>
    /// <param name="status">The stored status.</param>
    /// <param name="startDate">The start date.</param>
    /// <returns>The membership.</returns>
    public static Membership Restore(Guid id, Guid memberId, MembershipType type, MembershipStatus status, DateOnly startDate)
    {
        return new Membership(id, memberId, type, status, startDate);
    }
}
