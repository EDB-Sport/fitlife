using Bunit;
using MemberPortal.Web.Clients;
using MemberPortal.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPortal.Web.Tests;

/// <summary>The page <c>/log-ind</c> (#206). A successful log-in is in the integration tests, because it needs the request's cookie.</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class LogInTests : BunitContext
{
    [Test]
    public void LogIn_HasTheHeadingTheFieldsAndTheButton()
    {
        // Arrange
        Services.AddSingleton<IIdentityClient>(new FakeIdentityClient(token: null));

        // Act
        IRenderedComponent<LogIn> cut = Render<LogIn>();

        // Assert
        Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Log ind"));
        Assert.That(cut.Find("label[for=email]").TextContent, Is.EqualTo("E-mail"));
        Assert.That(cut.Find("label[for=password]").TextContent, Is.EqualTo("Adgangskode"));
        Assert.That(cut.Find("#password").GetAttribute("type"), Is.EqualTo("password"));
        Assert.That(cut.Find("button[type=submit]").TextContent, Is.EqualTo("Log ind"));
    }

    [Test]
    public void LogIn_WithWrongPassword_ShowsTheSentence()
    {
        // Arrange
        Services.AddSingleton<IIdentityClient>(new FakeIdentityClient(token: null));
        IRenderedComponent<LogIn> cut = Render<LogIn>();

        // Act
        Submit(cut);

        // Assert
        Assert.That(cut.Find("[role=alert]").TextContent, Is.EqualTo("Forkert e-mail eller adgangskode."));
    }

    [Test]
    public void LogIn_WhenIdentityIsUnavailable_ShowsTryAgainLater()
    {
        // Arrange
        Services.AddSingleton<IIdentityClient>(new FakeIdentityClient(new IdentityUnavailableException("Identity can't be reached.")));
        IRenderedComponent<LogIn> cut = Render<LogIn>();

        // Act
        Submit(cut);

        // Assert
        Assert.That(cut.Find("[role=alert]").TextContent, Is.EqualTo("Vi kan ikke logge dig ind lige nu. Prøv igen om lidt."));
    }

    [Test]
    public void LogIn_SendsTheEmailAndPasswordToIdentity()
    {
        // Arrange
        var identity = new FakeIdentityClient(token: null);
        Services.AddSingleton<IIdentityClient>(identity);
        IRenderedComponent<LogIn> cut = Render<LogIn>();

        // Act
        Submit(cut);

        // Assert
        Assert.That(identity.Request, Is.EqualTo(new LogInRequest("medlem@fitlife.test", "hemmelig")));
    }

    [Test]
    public void LogIn_WithoutAnEmail_DoesNotAskIdentity()
    {
        // Arrange
        var identity = new FakeIdentityClient(token: null);
        Services.AddSingleton<IIdentityClient>(identity);
        IRenderedComponent<LogIn> cut = Render<LogIn>();
        cut.Find("#password").Change("hemmelig");

        // Act
        cut.Find("form").Submit();

        // Assert
        Assert.That(identity.Request, Is.Null);
        Assert.That(cut.FindAll("[role=alert]"), Is.Empty);
    }

    private static void Submit(IRenderedComponent<LogIn> cut)
    {
        cut.Find("#email").Change("medlem@fitlife.test");
        cut.Find("#password").Change("hemmelig");
        cut.Find("form").Submit();
    }
}
