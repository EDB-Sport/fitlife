using AngleSharp.Dom;
using Bunit;
using MemberPortal.Web.Components.Pages;
using NUnit.Framework;

namespace MemberPortal.Web.Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ComponentTests : BunitContext
{
    [Test]
    public void Home_RendersHeadingWithFitLifeTitle()
    {
        // Arrange & Act
        IRenderedComponent<Home> cut = Render<Home>();

        // Assert
        IElement heading = cut.Find("h1");
        Assert.That(heading.TextContent, Is.EqualTo("Velkommen til FitLife Digital"));
    }

    [Test]
    public void Error_ShowsOneDanishLine()
    {
        // Arrange & Act
        IRenderedComponent<Error> cut = Render<Error>();

        // Assert
        Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Der skete en fejl. Prøv igen om lidt."));
    }

    [Test]
    public void Error_DoesNotShowTheDevelopmentAdvice()
    {
        // Arrange & Act
        IRenderedComponent<Error> cut = Render<Error>();

        // Assert
        Assert.That(cut.Markup, Does.Not.Contain("Development"));
        Assert.That(cut.Markup, Does.Not.Contain("ASPNETCORE_ENVIRONMENT"));
    }

    [Test]
    public void Error_ShowsTheRequestId()
    {
        // Arrange
        using var activity = new System.Diagnostics.Activity("request");
        activity.Start();

        // Act
        IRenderedComponent<Error> cut = Render<Error>();

        // Assert
        Assert.That(cut.Find("code").TextContent, Is.EqualTo(activity.Id));
    }
}
