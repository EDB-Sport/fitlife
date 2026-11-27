using Bunit;
using MemberPortal.Web.Clients;
using MemberPortal.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPortal.Web.Tests;

/// <summary>
/// The page <c>/bliv-medlem</c> (#209). Creating the account, logging in and joining in one go is in the integration
/// tests, because the log-in needs the request's cookie.
/// </summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class BecomeMemberTests : BunitContext
{
    private readonly FakeIdentityClient _identity = new(new TokenResponse("header.payload.signature", new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero)));
    private readonly FakeMembershipClient _membership = new();

    public BecomeMemberTests()
    {
        Services.AddSingleton<IIdentityClient>(_identity);
        Services.AddSingleton<IMembershipClient>(_membership);
    }

    [Test]
    public void BecomeMember_HasTheHeadingTheFieldsAndTheButton()
    {
        // Arrange
        AddAuthorization();

        // Act
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();

        // Assert
        Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Bliv medlem"));
        Assert.That(cut.Find("label[for=name]").TextContent, Is.EqualTo("Navn"));
        Assert.That(cut.Find("label[for=email]").TextContent, Is.EqualTo("E-mail"));
        Assert.That(cut.Find("label[for=password]").TextContent, Is.EqualTo("Adgangskode"));
        Assert.That(cut.Find("#password").GetAttribute("type"), Is.EqualTo("password"));
        Assert.That(cut.Find("button[type=submit]").TextContent, Is.EqualTo("Bliv medlem"));
    }

    [Test]
    public void BecomeMember_WhenTheEmailIsTaken_ShowsTheSentence()
    {
        // Arrange
        AddAuthorization();
        _identity.CreateAccountAnswer = CreateAccountAnswer.EmailAlreadyTaken;
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();

        // Act
        SubmitAll(cut);

        // Assert
        Assert.That(cut.Find("[role=alert]").TextContent.Trim(), Is.EqualTo("Der findes allerede en konto med den e-mail. Log ind i stedet."));
        Assert.That(cut.Find("[role=alert] a").GetAttribute("href"), Is.EqualTo("/log-ind"));
        Assert.That(_identity.Request, Is.Null, "Logged in although no account was made.");
        Assert.That(_membership.Calls, Is.Zero);
    }

    [Test]
    public void BecomeMember_WhenIdentityRefusesTheEmailOrPassword_ShowsTheSentence()
    {
        // Arrange
        AddAuthorization();
        _identity.CreateAccountAnswer = CreateAccountAnswer.Rejected;
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();

        // Act
        SubmitAll(cut);

        // Assert
        Assert.That(cut.Find("[role=alert]").TextContent, Is.EqualTo("Skriv en gyldig e-mail og en adgangskode på mindst 8 tegn."));
        Assert.That(_identity.Request, Is.Null);
        Assert.That(_membership.Calls, Is.Zero);
    }

    [Test]
    public void BecomeMember_SendsTheEmailAndPasswordToIdentity()
    {
        // Arrange
        AddAuthorization();
        _identity.CreateAccountAnswer = CreateAccountAnswer.EmailAlreadyTaken;
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();

        // Act
        SubmitAll(cut);

        // Assert
        Assert.That(_identity.CreateAccountRequest, Is.EqualTo(new CreateAccountRequest("medlem@fitlife.test", "hemmelig1")));
    }

    [Test]
    public void BecomeMember_WhenIdentityIsUnavailable_ShowsTheGenericSentence()
    {
        // Arrange
        AddAuthorization();
        Services.AddSingleton<IIdentityClient>(new FakeIdentityClient(new IdentityUnavailableException("Identity is not reachable.")));
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();

        // Act
        SubmitAll(cut);

        // Assert
        Assert.That(cut.Find("[role=alert]").TextContent, Is.EqualTo(ErrorSentences.Generic));
        Assert.That(_membership.Calls, Is.Zero);
    }

    [Test]
    public void BecomeMember_WhenLoggedIn_AsksOnlyForTheName()
    {
        // Arrange
        AddAuthorization().SetAuthorized("medlem@fitlife.test");

        // Act
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();

        // Assert
        Assert.That(cut.Find("label[for=name]").TextContent, Is.EqualTo("Navn"));
        Assert.That(cut.FindAll("#email"), Is.Empty);
        Assert.That(cut.FindAll("#password"), Is.Empty);
        Assert.That(cut.Find("button[type=submit]").TextContent, Is.EqualTo("Bliv medlem"));
    }

    [Test]
    public void BecomeMember_WhenLoggedIn_OnlyJoinsWithTheCookiesToken()
    {
        // Arrange
        AddAuthorization().SetAuthorized("medlem@fitlife.test");
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();

        // Act
        SubmitName(cut);

        // Assert: no token passed in, so the bearer handler adds the cookie's
        Assert.That(_identity.CreateAccountRequest, Is.Null);
        Assert.That(_identity.Request, Is.Null);
        Assert.That(_membership.Calls, Is.EqualTo(1));
        Assert.That(_membership.AccessToken, Is.Null);
    }

    [Test]
    public void BecomeMember_WhenAlreadyAMember_ShowsTheSentence()
    {
        // Arrange
        AddAuthorization().SetAuthorized("medlem@fitlife.test");
        _membership.Answer = JoinMembershipAnswer.AlreadyAMember;
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();

        // Act
        SubmitName(cut);

        // Assert
        Assert.That(cut.Find("[role=alert]").TextContent, Is.EqualTo("Du er allerede medlem."));
    }

    [Test]
    public void BecomeMember_WhenTheFirstFeeIsNotPaid_ShowsThatPaymentIsMissing()
    {
        // Arrange
        AddAuthorization().SetAuthorized("medlem@fitlife.test");
        _membership.Answer = JoinMembershipAnswer.PaymentMissing;
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/bliv-medlem");

        // Act
        SubmitName(cut);

        // Assert: it stays on the page, with the sentence and no form
        Assert.That(cut.Find("[role=status]").TextContent, Is.EqualTo("Du er meldt ind, men betalingen mangler. Du kan ikke booke, før den er betalt."));
        Assert.That(cut.FindAll("form"), Is.Empty);
        Assert.That(navigation.Uri, Is.EqualTo(navigation.BaseUri + "bliv-medlem"), "It left the page.");
    }

    [Test]
    public void BecomeMember_WhenTheMembershipIsActive_GoesToTheFrontPage()
    {
        // Arrange
        AddAuthorization().SetAuthorized("medlem@fitlife.test");
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/bliv-medlem");

        // Act
        SubmitName(cut);

        // Assert
        Assert.That(navigation.Uri, Is.EqualTo(navigation.BaseUri));
    }

    [Test]
    public void BecomeMember_WhenLoggedInAndMembershipIsUnavailable_ShowsTheGenericSentence()
    {
        // Arrange
        AddAuthorization().SetAuthorized("medlem@fitlife.test");
        _membership.Failure = new MembershipUnavailableException("Membership is not reachable.");
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();

        // Act
        SubmitName(cut);

        // Assert
        Assert.That(cut.Find("[role=alert]").TextContent, Is.EqualTo(ErrorSentences.Generic));
    }

    [Test]
    public void BecomeMember_WithoutAName_CallsNothing()
    {
        // Arrange
        AddAuthorization();
        IRenderedComponent<BecomeMember> cut = Render<BecomeMember>();
        cut.Find("#email").Change("medlem@fitlife.test");
        cut.Find("#password").Change("hemmelig1");

        // Act
        cut.Find("form").Submit();

        // Assert
        Assert.That(_identity.CreateAccountRequest, Is.Null);
        Assert.That(_membership.Calls, Is.Zero);
    }

    private static void SubmitAll(IRenderedComponent<BecomeMember> cut)
    {
        cut.Find("#name").Change("Mette Medlem");
        cut.Find("#email").Change("medlem@fitlife.test");
        cut.Find("#password").Change("hemmelig1");
        cut.Find("form").Submit();
    }

    private static void SubmitName(IRenderedComponent<BecomeMember> cut)
    {
        cut.Find("#name").Change("Mette Medlem");
        cut.Find("form").Submit();
    }
}
