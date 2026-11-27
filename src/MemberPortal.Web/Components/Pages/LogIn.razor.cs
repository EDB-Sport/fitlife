using System.ComponentModel.DataAnnotations;
using MemberPortal.Web.Authentication;
using MemberPortal.Web.Clients;
using Microsoft.AspNetCore.Components;

namespace MemberPortal.Web.Components.Pages;

/// <summary>
/// The page <c>/log-ind</c> (#206): gets a token from Identity and signs in with the cookie. It renders statically,
/// because Interactive Server components have no <see cref="HttpContext"/> to set the cookie on.
/// </summary>
/// <param name="identityClient">Identity, through the gateway.</param>
/// <param name="navigation">Redirects to the front page after logging in.</param>
public partial class LogIn(IIdentityClient identityClient, NavigationManager navigation)
{
    /// <summary>Shown when Identity answers 401.</summary>
    public const string WrongLogInSentence = "Forkert e-mail eller adgangskode.";

    /// <summary>Shown when Identity answers another error, or nothing.</summary>
    public const string TryAgainLaterSentence = "Vi kan ikke logge dig ind lige nu. Prøv igen om lidt.";

    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    // Set from the posted form; null on the first GET, and then Fields makes an empty one
    [SupplyParameterFromForm]
    private LogInForm? Form { get; set; }

    private LogInForm Fields => Form ??= new LogInForm();

    private string? ErrorSentence { get; set; }

    private async Task LogInAsync()
    {
        TokenResponse? token;
        try
        {
            token = await identityClient.LogInAsync(new LogInRequest(Fields.Email, Fields.Password), CancellationToken.None).ConfigureAwait(false);
        }
        catch (IdentityUnavailableException)
        {
            ErrorSentence = TryAgainLaterSentence;
            return;
        }

        if (token is null)
        {
            ErrorSentence = WrongLogInSentence;
            return;
        }

        HttpContext context = HttpContext ?? throw new InvalidOperationException("Found no HttpContext. Keep /log-ind statically rendered: an interactive page can't set the cookie.");
        await PortalAuthentication.SignInAsync(context, Fields.Email, token).ConfigureAwait(false);
        navigation.NavigateTo(PortalAuthentication.FrontPagePath);
    }

    /// <summary>The fields of the log-in form.</summary>
    internal sealed class LogInForm
    {
        [Required(ErrorMessage = "Skriv din e-mail.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Skriv din adgangskode.")]
        public string Password { get; set; } = string.Empty;
    }
}
