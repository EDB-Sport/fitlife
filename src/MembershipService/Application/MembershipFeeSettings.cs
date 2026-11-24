namespace MembershipService.Application;

/// <summary>The membership fee, from configuration section <c>Membership</c>. The case gives no price (#174).</summary>
public sealed class MembershipFeeSettings
{
    /// <summary>The configuration section.</summary>
    public const string Section = "Membership";

    /// <summary>The monthly fee in Danish kroner.</summary>
    public decimal MonthlyFeeDkk { get; init; }
}
