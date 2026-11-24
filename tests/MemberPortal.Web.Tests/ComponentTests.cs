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
}
