namespace MembershipService.Application;

/// <summary>The membership fee, from configuration section <c>Membership</c>. The case has one membership type at 350 kr. a month (#174).</summary>
public sealed class MembershipFeeSettings
{
    /// <summary>The configuration section.</summary>
    public const string Section = "Membership";

    /// <summary>The monthly fee in Danish kroner.</summary>
    public decimal MonthlyFeeDkk { get; init; }
}
