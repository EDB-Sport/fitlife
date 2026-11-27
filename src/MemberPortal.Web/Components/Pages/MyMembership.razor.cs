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

    private MyMembershipResponse? Membership { get; set; }

    private string? ErrorSentence { get; set; }


    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Membership = await membershipClient.GetMyMembershipAsync(CancellationToken.None).ConfigureAwait(false);
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
}
