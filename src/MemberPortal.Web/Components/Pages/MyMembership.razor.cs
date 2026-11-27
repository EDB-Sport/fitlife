using System.ComponentModel.DataAnnotations;
using System.Globalization;
using MemberPortal.Web.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace MemberPortal.Web.Components.Pages;

/// <summary>
/// The page <c>/mit-medlemskab</c> (#624): the logged-in member's membership type, start date and status, "Betal nu"
/// (#535), and pausing and ending the pause (#211). Every rule about the dates is Membership's. It renders
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

    /// <summary>Shown when the pause is set (#211).</summary>
    public const string PausedSentence = "Dit medlemskab er sat på pause.";

    /// <summary>Shown when the pause is ended (#211).</summary>
    public const string PauseEndedSentence = "Din pause er afsluttet.";

    // MembershipStatus in contracts/openapi/membershipservice.json
    private const string PaymentMissing = "PaymentMissing";
    private const string Active = "Active";
    private const string Paused = "Paused";

    private MyMembershipResponse? Membership { get; set; }

    private string? ErrorSentence { get; set; }

    private string? ConfirmationSentence { get; set; }

    // Membership answered 404: the account has not joined
    private bool IsNotAMember { get; set; }

    // The pause period, from an answer to pausing: GET /members/me doesn't send it yet (#637)
    private DateOnly? PausedFrom { get; set; }

    private DateOnly? PausedTo { get; set; }

    // Set from the posted form; null on a GET, and then PauseFields makes an empty one
    [SupplyParameterFromForm(FormName = "pause")]
    private PauseForm? PauseFormValues { get; set; }

    private PauseForm PauseFields => PauseFormValues ??= new PauseForm();

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

    private static bool CanPause(MyMembershipResponse membership)
    {
        return string.Equals(membership.MembershipStatus, Active, StringComparison.Ordinal);
    }

    private static string Day(DateOnly day)
    {
        return day.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
    }

    // A period from Membership's answer, or the status Pause when the page has no period (#637)
    private bool CanEndPause(MyMembershipResponse membership)
    {
        return PausedFrom is not null || string.Equals(membership.MembershipStatus, Paused, StringComparison.Ordinal);
    }

    // "Sæt på pause": the form's validation has checked that both days are filled in, and nothing else
    private async Task PauseAsync()
    {
        var request = new PauseRequest(PauseFields.From.GetValueOrDefault(), PauseFields.To.GetValueOrDefault());
        await ShowPauseAnswerAsync(() => membershipClient.PauseAsync(request, CancellationToken.None), PausedSentence).ConfigureAwait(false);
    }

    private async Task EndPauseAsync()
    {
        await ShowPauseAnswerAsync(() => membershipClient.EndPauseAsync(CancellationToken.None), PauseEndedSentence).ConfigureAwait(false);
    }

    // The membership from the answer and the confirmation, or the code's sentence and the membership as it was
    private async Task ShowPauseAnswerAsync(Func<Task<PauseAnswer>> call, string confirmation)
    {
        PauseAnswer answer;
        try
        {
            answer = await call().ConfigureAwait(false);
        }
        catch (MembershipUnavailableException)
        {
            ErrorSentence = ErrorSentences.Generic;
            return;
        }

        if (answer.Membership is not MembershipPauseResponse membership)
        {
            ErrorSentence = ErrorSentences.ForCode(answer.RejectionCode);
            return;
        }

        Membership = new MyMembershipResponse(membership.MemberId, membership.MembershipType, membership.MembershipStatus, membership.IsActive, membership.StartDate);
        PausedFrom = membership.PausedFrom;
        PausedTo = membership.PausedTo;
        ConfirmationSentence = confirmation;
    }

    /// <summary>The fields of the pause form: both days must be filled in.</summary>
    internal sealed class PauseForm
    {
        [Required(ErrorMessage = "Vælg den første dag i pausen.")]
        public DateOnly? From { get; set; }

        [Required(ErrorMessage = "Vælg den sidste dag i pausen.")]
        public DateOnly? To { get; set; }
    }
}
