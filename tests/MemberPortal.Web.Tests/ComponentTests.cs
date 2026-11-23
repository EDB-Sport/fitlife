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
        var cut = Render<Home>();

        // Assert
        var heading = cut.Find("h1");
        heading.MarkupMatches("<h1 class=\"display-5 fw-bold mb-3\">Velkommen til FitLife Digital</h1>");
    }

    [Test]
    public void Home_RendersLocalFriendlyBadge()
    {
        // Arrange & Act
        var cut = Render<Home>();

        // Assert
        var badge = cut.Find("span.badge");
        Assert.That(badge.TextContent, Does.Contain("Lokal & venlig"));
    }

    [Test]
    public void Home_RendersThreeFeatureCards()
    {
        // Arrange & Act
        var cut = Render<Home>();

        // Assert
        var cards = cut.FindAll(".card");
        Assert.That(cards.Count, Is.EqualTo(3));
    }
}
