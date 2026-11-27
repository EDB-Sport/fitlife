using System.Globalization;
using MemberPortal.Web.Clients;
using Microsoft.AspNetCore.Authorization;

namespace MemberPortal.Web.Components.Pages;

/// <summary>
/// The page <c>/mit-medlemskab</c> (#624): the logged-in member's membership type, start date and status. It renders
/// statically, because the <see cref="BearerTokenHandler"/> needs the request's <see cref="HttpContext"/>.
/// </summary>
/// <param name="membershipClient">Membership, through the gateway, with the cookie's token.</param>
[Authorize]
public partial class MyMembership(IMembershipClient membershipClient)
{
    /// <summary>Shown when the account has not joined; the page adds the link to <c>/bliv-medlem</c>.</summary>
    public const string NotAMemberSentence = "Du er ikke medlem endnu.";

    /// <summary>Shown when the missing fee is paid (#535).</summary>
    public const string PaymentReceivedSentence = "Din betaling er modtaget.";

    // MembershipStatus.PaymentMissing in contracts/openapi/membershipservice.json
    private const string PaymentMissing = "PaymentMissing";

    private MyMembershipResponse? Membership { get; set; }

    private string? ErrorSentence { get; set; }

    private string? ConfirmationSentence { get; set; }

    // Membership answered 404: the account has not joined
    private bool IsNotAMember { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Membership = await membershipClient.GetMyMembershipAsync(CancellationToken.None).ConfigureAwait(false);
            IsNotAMember = Membership is null;
        }
        catch (MembershipUnavailableException)
        {
            ErrorSentence = ErrorSentences.Generic;
        }
    }

    private static string StartDate(MyMembershipResponse membership)
    {
        return membership.StartDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
    }

    private static string Status(MyMembershipResponse membership)
    {
        return MembershipStatusText.InDanish(membership.MembershipStatus);
    }

    private static bool CanPay(MyMembershipResponse membership)
    {
        return string.Equals(membership.MembershipStatus, PaymentMissing, StringComparison.Ordinal);
    }

    // "Betal nu": the membership from the answer, or the code's sentence and the membership as it was
    private async Task PayAsync()
    {
        PaymentAnswer answer;
        try
        {
            answer = await membershipClient.PayAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (MembershipUnavailableException)
        {
            ErrorSentence = ErrorSentences.Generic;
            return;
        }

        if (answer.Membership is null)
        {
            ErrorSentence = ErrorSentences.ForCode(answer.RejectionCode);
            return;
        }

        Membership = answer.Membership;
        ConfirmationSentence = PaymentReceivedSentence;
    }
}
