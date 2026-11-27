using System.ComponentModel.DataAnnotations;
using MemberPortal.Web.Authentication;
using MemberPortal.Web.Clients;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace MemberPortal.Web.Components.Pages;

/// <summary>
/// The page <c>/bliv-medlem</c> (#209): a person becomes a member with one form. Behind it are the two steps of the
/// context map: an account in Identity, then joining in Membership with the token. A logged-in person only joins.
/// It renders statically, because Interactive Server components have no <see cref="HttpContext"/> to set the cookie on.
/// </summary>
/// <param name="identityClient">Identity, through the gateway.</param>
/// <param name="membershipClient">Membership, through the gateway.</param>
/// <param name="navigation">Redirects to the front page after joining.</param>
public partial class BecomeMember(IIdentityClient identityClient, IMembershipClient membershipClient, NavigationManager navigation)
{
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    // Set from the posted form; null on the first GET, and then Fields makes an empty one
    [SupplyParameterFromForm]
    private BecomeMemberForm? Form { get; set; }

    private BecomeMemberForm Fields => Form ??= new BecomeMemberForm();

    // Logged in with the cookie, or just now in this request
    private bool IsLoggedIn { get; set; }

    private string? ErrorSentence { get; set; }

    private bool ShowLogInLink { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (AuthenticationState is not null)
        {
            AuthenticationState state = await AuthenticationState.ConfigureAwait(false);
            IsLoggedIn = state.User.Identity?.IsAuthenticated == true;
        }
    }

    // The input renders empty, so the value goes one way only: from the input to the form
    private void SetPassword(string? password)
    {
        Fields.Password = password ?? string.Empty;
    }

    private async Task BecomeMemberAsync()
    {
        string? accessToken = null;
        if (!IsLoggedIn)
        {
            TokenResponse? token = await CreateAccountAndLogInAsync(Fields.Password).ConfigureAwait(false);
            if (token is null)
            {
                return;
            }

            accessToken = token.AccessToken;
        }

        if (await JoinAsync(accessToken).ConfigureAwait(false))
        {
            navigation.NavigateTo(PortalAuthentication.FrontPagePath);
        }
    }

    // The account and the log-in. Null, with the sentence set, when either failed
    private async Task<TokenResponse?> CreateAccountAndLogInAsync(string password)
    {
        TokenResponse? token;
        try
        {
            CreateAccountAnswer answer = await identityClient.CreateAccountAsync(new CreateAccountRequest(Fields.Email, password), CancellationToken.None).ConfigureAwait(false);
            switch (answer)
            {
                case CreateAccountAnswer.EmailAlreadyTaken:
                    ErrorSentence = ErrorSentences.EmailAlreadyTaken;
                    ShowLogInLink = true;
                    return null;
                case CreateAccountAnswer.Rejected:
                    ErrorSentence = ErrorSentences.InvalidNewAccount;
                    return null;
                default:
                    break;
            }

            token = await identityClient.LogInAsync(new LogInRequest(Fields.Email, password), CancellationToken.None).ConfigureAwait(false);
        }
        catch (IdentityUnavailableException)
        {
            ErrorSentence = ErrorSentences.Generic;
            return null;
        }

        if (token is null)
        {
            ErrorSentence = ErrorSentences.Generic;
            return null;
        }

        HttpContext context = HttpContext ?? throw new InvalidOperationException("Found no HttpContext. Keep /bliv-medlem statically rendered: an interactive page can't set the cookie.");
        await PortalAuthentication.SignInAsync(context, Fields.Email, token).ConfigureAwait(false);
        IsLoggedIn = true;
        return token;
    }

    // Joins with the fresh token, or with the cookie's when accessToken is null. False, with the sentence set, when it failed
    private async Task<bool> JoinAsync(string? accessToken)
    {
        JoinMembershipAnswer answer;
        try
        {
            answer = await membershipClient.JoinAsync(accessToken, CancellationToken.None).ConfigureAwait(false);
        }
        catch (MembershipUnavailableException)
        {
            if (accessToken is null)
            {
                ErrorSentence = ErrorSentences.Generic;
            }
            else
            {
                ErrorSentence = ErrorSentences.AccountMadeButNotJoined;
            }

            return false;
        }

        if (answer == JoinMembershipAnswer.AlreadyAMember)
        {
            ErrorSentence = ErrorSentences.AlreadyAMember;
            return false;
        }

        return true;
    }

    /// <summary>The fields of the form. A logged-in person fills in only the name.</summary>
    internal sealed class BecomeMemberForm
    {
        // No contract takes the name yet (identityservice.json, membershipservice.json), so it isn't sent
        [Required(ErrorMessage = "Skriv dit navn.")]
        public string Name { get; set; } = string.Empty;

        // Identity checks the e-mail address and the password (#234), not the portal
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
