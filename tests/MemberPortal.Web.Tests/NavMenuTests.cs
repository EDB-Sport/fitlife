using Bunit;
using MemberPortal.Web.Components.Layout;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPortal.Web.Tests;

/// <summary>The nav menu says who is logged in, and logs in or out (#206).</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class NavMenuTests : BunitContext
{
    public NavMenuTests()
    {
        Services.AddSingleton<AntiforgeryStateProvider>(new FakeAntiforgery());
    }

    [Test]
    public void NavMenu_WhenNotLoggedIn_ShowsLogInd()
    {
        // Arrange
        AddAuthorization();

        // Act
        IRenderedComponent<NavMenu> cut = Render<NavMenu>();

        // Assert
        Assert.That(cut.Find("a[href=log-ind]").TextContent.Trim(), Is.EqualTo("Log ind"));
        Assert.That(cut.FindAll("form[action=log-ud]"), Is.Empty);
    }

    [Test]
    public void NavMenu_WhenLoggedIn_ShowsTheEmailAndLogUd()
    {
        // Arrange
        AddAuthorization().SetAuthorized("medlem@fitlife.test");

        // Act
        IRenderedComponent<NavMenu> cut = Render<NavMenu>();

        // Assert
        Assert.That(cut.Find(".logged-in-email").TextContent, Is.EqualTo("medlem@fitlife.test"));
        Assert.That(cut.Find("form[action=log-ud] button[type=submit]").TextContent, Is.EqualTo("Log ud"));
        Assert.That(cut.FindAll("a[href=log-ind]"), Is.Empty);
    }

    [Test]
    public void NavMenu_LogUd_PostsWithTheAntiforgeryToken()
    {
        // Arrange
        AddAuthorization().SetAuthorized("medlem@fitlife.test");

        // Act
        IRenderedComponent<NavMenu> cut = Render<NavMenu>();

        // Assert
        Assert.That(cut.Find("form[action=log-ud]").GetAttribute("method"), Is.EqualTo("post"));
        Assert.That(cut.Find("form[action=log-ud] input[type=hidden]").GetAttribute("value"), Is.EqualTo(FakeAntiforgery.Value));
    }

    private sealed class FakeAntiforgery : AntiforgeryStateProvider
    {
        public const string Value = "antiforgery-test-value";

        public override AntiforgeryRequestToken GetAntiforgeryToken()
        {
            return new AntiforgeryRequestToken(Value, "__RequestVerificationToken");
        }
    }
}
