using Bunit;
using MemberPortal.Web.Clients;
using MemberPortal.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPortal.Web.Tests;

/// <summary>
/// The page <c>/mit-medlemskab</c>, the status (#624). That it needs a log-in is in the integration tests, because only
/// the real pipeline reads <c>[Authorize]</c>.
/// </summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class MyMembershipTests : BunitContext
{
    private readonly FakeMembershipClient _membership = new();

    public MyMembershipTests()
    {
        Services.AddSingleton<IMembershipClient>(_membership);
    }

    [TestCase("Active", "Aktiv")]
    [TestCase("Paused", "Pause")]
    [TestCase("PaymentMissing", "Betaling mangler")]
    [TestCase("Cancelled", "Opsagt")]
    [TestCase("Ended", "Afsluttet")]
    public void MyMembership_ForAMember_ShowsTheStatusInDanish(string status, string danish)
    {
        // Arrange
        _membership.MyMembership = Membership(status);

        // Act
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Assert
        Assert.That(Text(cut, "#membership-status"), Is.EqualTo(danish));
    }

    [Test]
    public void MyMembership_WithAStatusThePortalDoesNotKnow_ShowsItAsItComes()
    {
        // Arrange
        _membership.MyMembership = Membership("Frozen");

        // Act
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Assert
        Assert.That(Text(cut, "#membership-status"), Is.EqualTo("Frozen"));
    }

    [Test]
    public void MyMembership_ForAMember_ShowsTheHeadingTheTypeAndTheStartDate()
    {
        // Arrange
        _membership.MyMembership = Membership("Active");

        // Act
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Assert
        Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Mit medlemskab"));
        Assert.That(string.Join(" | ", cut.FindAll("dt").Select(term => term.TextContent.Trim())), Is.EqualTo("Medlemstype | Startdato | Status"));
        Assert.That(Text(cut, "#membership-type"), Is.EqualTo("Standard"));
        Assert.That(Text(cut, "#membership-start-date"), Is.EqualTo("03-11-2026"));
    }

    [Test]
    public void MyMembership_WhenTheAccountHasNotJoined_ShowsTheLinkToBlivMedlem()
    {
        // Arrange: Membership's 404, no member for the account
        _membership.MyMembership = null;

        // Act
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Assert
        Assert.That(Text(cut, ".not-a-member"), Does.StartWith("Du er ikke medlem endnu."));
        Assert.That(cut.Find(".not-a-member a").GetAttribute("href"), Is.EqualTo("/bliv-medlem"));
        Assert.That(cut.Find(".not-a-member a").TextContent, Is.EqualTo("Bliv medlem"));
        Assert.That(cut.FindAll("dl"), Is.Empty);
    }

    [Test]
    public void MyMembership_WhenTheCallFails_ShowsTheGenericSentence()
    {
        // Arrange
        _membership.Failure = new MembershipUnavailableException("Membership answered 500: System.InvalidOperationException");

        // Act
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Assert: the sentence and nothing from the error
        Assert.That(Text(cut, "[role=alert]"), Is.EqualTo("Det lykkedes ikke. Prøv igen om lidt."));
        Assert.That(cut.Markup, Does.Not.Contain("500").And.Not.Contain("Exception"));
        Assert.That(cut.FindAll("dl"), Is.Empty);
        Assert.That(cut.FindAll(".not-a-member"), Is.Empty);
    }

    private static MyMembershipResponse Membership(string status)
    {
        return new MyMembershipResponse(Guid.NewGuid(), "Standard", status, IsActive: true, new DateOnly(2026, 11, 3));
    }

    private static string Text(IRenderedComponent<MyMembership> cut, string selector)
    {
        return cut.Find(selector).TextContent.Trim();
    }
}
