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

    [Test]
    public void MyMembership_WithPaymentMissing_ShowsTheBetalButton()
    {
        // Arrange
        _membership.MyMembership = Membership("PaymentMissing");

        // Act
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Assert
        Assert.That(cut.Find("form.pay button[type=submit]").TextContent.Trim(), Is.EqualTo("Betal nu"));
        Assert.That(cut.Find("form.pay").GetAttribute("method"), Is.EqualTo("post"));
    }

    [TestCase("Active")]
    [TestCase("Paused")]
    [TestCase("Cancelled")]
    [TestCase("Ended")]
    public void MyMembership_WithAnyOtherStatus_ShowsNoBetalButton(string status)
    {
        // Arrange
        _membership.MyMembership = Membership(status);

        // Act
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Assert
        Assert.That(cut.FindAll("form.pay"), Is.Empty);
    }

    [Test]
    public void MyMembership_WhenThePaymentSucceeds_ShowsTheMembershipFromTheAnswer()
    {
        // Arrange
        _membership.MyMembership = Membership("PaymentMissing");
        _membership.PaymentAnswer = new PaymentAnswer(Membership("Active"), RejectionCode: null);
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Act
        cut.Find("form.pay").Submit();

        // Assert
        Assert.That(_membership.PayCalls, Is.EqualTo(1));
        Assert.That(Text(cut, "[role=status]"), Is.EqualTo("Din betaling er modtaget."));
        Assert.That(Text(cut, "#membership-status"), Is.EqualTo("Aktiv"));
        Assert.That(cut.FindAll("form.pay"), Is.Empty);
    }

    [TestCase("ChargeDeclined", "Betalingen blev afvist. Prøv igen senere.")]
    [TestCase("NothingIsOwed", "Du skylder ikke noget.")]
    [TestCase("SomethingElse", "Det lykkedes ikke. Prøv igen om lidt.")]
    public void MyMembership_WhenTheChargeIsDeclined_ShowsTheSentenceForTheCode(string code, string sentence)
    {
        // Arrange
        _membership.MyMembership = Membership("PaymentMissing");
        _membership.PaymentAnswer = new PaymentAnswer(Membership: null, code);
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Act
        cut.Find("form.pay").Submit();

        // Assert: the sentence, and the membership as it was
        Assert.That(Text(cut, "[role=alert]"), Is.EqualTo(sentence));
        Assert.That(Text(cut, "#membership-status"), Is.EqualTo("Betaling mangler"));
        Assert.That(cut.Markup, Does.Not.Contain(code));
    }

    [Test]
    public void MyMembership_WhenThePaymentGetsNoAnswer_ShowsTheGenericSentence()
    {
        // Arrange
        _membership.MyMembership = Membership("PaymentMissing");
        _membership.PaymentFailure = new MembershipUnavailableException("Membership can't be reached.");
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Act
        cut.Find("form.pay").Submit();

        // Assert
        Assert.That(Text(cut, "[role=alert]"), Is.EqualTo("Det lykkedes ikke. Prøv igen om lidt."));
        Assert.That(Text(cut, "#membership-status"), Is.EqualTo("Betaling mangler"));
    }

    [Test]
    public void MyMembership_ForAnActiveMembership_ShowsThePauseForm()
    {
        // Arrange
        _membership.MyMembership = Membership("Active");

        // Act
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Assert
        Assert.That(Text(cut, "h2.pause-heading"), Is.EqualTo("Pause"));
        Assert.That(Text(cut, "label[for=pause-from]"), Is.EqualTo("Pause fra"));
        Assert.That(Text(cut, "label[for=pause-to]"), Is.EqualTo("Pause til"));
        Assert.That(cut.Find("#pause-from").GetAttribute("type"), Is.EqualTo("date"));
        Assert.That(cut.Find("#pause-to").GetAttribute("type"), Is.EqualTo("date"));
        Assert.That(Text(cut, "form.pause button[type=submit]"), Is.EqualTo("Sæt på pause"));
    }

    [TestCase("Paused")]
    [TestCase("PaymentMissing")]
    [TestCase("Cancelled")]
    [TestCase("Ended")]
    public void MyMembership_WhenNotActive_ShowsNoPauseForm(string status)
    {
        // Arrange
        _membership.MyMembership = Membership(status);

        // Act
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Assert
        Assert.That(cut.FindAll("form.pause"), Is.Empty);
        Assert.That(cut.FindAll("#pause-from"), Is.Empty);
    }

    [Test]
    public void MyMembership_WhenThePauseIsSet_ShowsTheMembershipFromTheAnswer()
    {
        // Arrange
        _membership.MyMembership = Membership("Active");
        _membership.PauseAnswer = new PauseAnswer(PauseMembership("Paused", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), RejectionCode: null);
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Act
        SetPause(cut, "2026-12-01", "2026-12-31");

        // Assert
        Assert.That(_membership.PauseRequest, Is.EqualTo(new PauseRequest(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31))));
        Assert.That(Text(cut, "[role=status]"), Is.EqualTo("Dit medlemskab er sat på pause."));
        Assert.That(Text(cut, "#membership-status"), Is.EqualTo("Pause"));
    }

    [Test]
    public void MyMembership_WithAPausePeriod_ShowsThePeriodAndAfslutPause()
    {
        // Arrange: a pause that starts later, so the status is still Active
        _membership.MyMembership = Membership("Active");
        _membership.PauseAnswer = new PauseAnswer(PauseMembership("Active", new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 17)), RejectionCode: null);
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Act
        SetPause(cut, "2027-01-04", "2027-01-17");

        // Assert
        Assert.That(Text(cut, ".pause-period"), Is.EqualTo("Pause fra 04-01-2027 til 17-01-2027"));
        Assert.That(Text(cut, "form.end-pause button[type=submit]"), Is.EqualTo("Afslut pause"));
    }

    [Test]
    public void MyMembership_WhenPaused_ShowsAfslutPause()
    {
        // Arrange: GET /members/me has no pause period yet (#637), but the status says Pause
        _membership.MyMembership = Membership("Paused");

        // Act
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Assert
        Assert.That(Text(cut, "form.end-pause button[type=submit]"), Is.EqualTo("Afslut pause"));
        Assert.That(cut.FindAll(".pause-period"), Is.Empty);
    }

    [Test]
    public void MyMembership_WithoutAPause_ShowsNoAfslutPause()
    {
        // Arrange
        _membership.MyMembership = Membership("Active");

        // Act
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Assert
        Assert.That(cut.FindAll("form.end-pause"), Is.Empty);
    }

    [Test]
    public void MyMembership_WhenThePauseIsEnded_ShowsTheConfirmation()
    {
        // Arrange
        _membership.MyMembership = Membership("Paused");
        _membership.EndPauseAnswer = new PauseAnswer(PauseMembership("Active", pausedFrom: null, pausedTo: null), RejectionCode: null);
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Act
        cut.Find("form.end-pause").Submit();

        // Assert
        Assert.That(_membership.EndPauseCalls, Is.EqualTo(1));
        Assert.That(Text(cut, "[role=status]"), Is.EqualTo("Din pause er afsluttet."));
        Assert.That(Text(cut, "#membership-status"), Is.EqualTo("Aktiv"));
        Assert.That(cut.FindAll("form.end-pause"), Is.Empty);
    }

    [TestCase("PauseStartsInThePast", "Pausen kan ikke starte før i dag.")]
    [TestCase("PauseEndsBeforeItStarts", "Pausen skal slutte efter den dag, den starter.")]
    [TestCase("MembershipNotActive", "Dit medlemskab er ikke aktivt.")]
    public void MyMembership_WhenThePauseIsRejected_ShowsTheSentenceForTheCode(string code, string sentence)
    {
        // Arrange
        _membership.MyMembership = Membership("Active");
        _membership.PauseAnswer = new PauseAnswer(Membership: null, code);
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Act
        SetPause(cut, "2026-12-01", "2026-12-31");

        // Assert: the sentence, and the membership as it was
        Assert.That(Text(cut, "[role=alert]"), Is.EqualTo(sentence));
        Assert.That(Text(cut, "#membership-status"), Is.EqualTo("Aktiv"));
    }

    [Test]
    public void MyMembership_WhenThereIsNoPauseToEnd_ShowsTheSentence()
    {
        // Arrange
        _membership.MyMembership = Membership("Paused");
        _membership.EndPauseAnswer = new PauseAnswer(Membership: null, "NoPauseToEnd");
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Act
        cut.Find("form.end-pause").Submit();

        // Assert
        Assert.That(Text(cut, "[role=alert]"), Is.EqualTo("Du har ingen pause at afslutte."));
    }

    [Test]
    public void MyMembership_WhenTheCodeIsUnknown_ShowsTheGenericSentence()
    {
        // Arrange
        _membership.MyMembership = Membership("Active");
        _membership.PauseAnswer = new PauseAnswer(Membership: null, "SomethingNew");
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();

        // Act
        SetPause(cut, "2026-12-01", "2026-12-31");

        // Assert
        Assert.That(Text(cut, "[role=alert]"), Is.EqualTo("Det lykkedes ikke. Prøv igen om lidt."));
    }

    [Test]
    public void MyMembership_WithoutBothDates_DoesNotAskMembership()
    {
        // Arrange
        _membership.MyMembership = Membership("Active");
        IRenderedComponent<MyMembership> cut = Render<MyMembership>();
        cut.Find("#pause-from").Change("2026-12-01");

        // Act
        cut.Find("form.pause").Submit();

        // Assert
        Assert.That(_membership.PauseRequest, Is.Null);
        Assert.That(cut.FindAll("[role=alert]"), Is.Empty);
    }

    private static void SetPause(IRenderedComponent<MyMembership> cut, string from, string to)
    {
        cut.Find("#pause-from").Change(from);
        cut.Find("#pause-to").Change(to);
        cut.Find("form.pause").Submit();
    }

    private static MembershipPauseResponse PauseMembership(string status, DateOnly? pausedFrom, DateOnly? pausedTo)
    {
        return new MembershipPauseResponse(Guid.NewGuid(), "Standard", status, IsActive: true, new DateOnly(2026, 11, 3), pausedFrom, pausedTo);
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
