using Bunit;
using MemberPortal.Web.Components.Pages;
using NUnit.Framework;

namespace MemberPortal.Web.Tests;

public class ComponentTests : BunitContext
{
    [Test]
    public void Home_RendersHeading()
    {
        // Arrange & Act
        var cut = Render<Home>();

        // Assert
        cut.Find("h1").MarkupMatches("<h1>Hello, world!</h1>");
    }

    [Test]
    public void Counter_IncrementsWhenClicked()
    {
        // Arrange
        var cut = Render<Counter>();

        // Act
        cut.Find("button").Click();

        // Assert
        cut.Find("p").MarkupMatches("<p role=\"status\">Current count: 1</p>");
    }
}
