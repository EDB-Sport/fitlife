using AngleSharp.Dom;
using Bunit;
using MemberPortal.Web.Clients;
using MemberPortal.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MemberPortal.Web.Tests;

/// <summary>The class schedule at <c>/hold</c>, without logging in (#205).</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class ClassScheduleTests : BunitContext
{
    private static readonly Guid Aarhus = new("6a0c3f4e-1b2d-4c5e-8f90-1a2b3c4d5e01");
    private static readonly Guid Odense = new("6a0c3f4e-1b2d-4c5e-8f90-1a2b3c4d5e02");

    private readonly FakeBookingClient _booking = new();

    public ClassScheduleTests()
    {
        Services.AddSingleton<IBookingClient>(_booking);
    }

    [Test]
    public void ClassSchedule_HasTheHeadingAndTheColumns()
    {
        // Arrange
        _booking.ClassSessions = [Session(At(1, 17, 30))];

        // Act
        IRenderedComponent<ClassSchedule> cut = Render<ClassSchedule>();

        // Assert
        Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Hold"));
        Assert.That(Joined(cut.FindAll("th")), Is.EqualTo("Tid | Hold | Center | Instruktør | Ledige pladser"));
    }

    [Test]
    public void ClassSchedule_WithClassSessions_ShowsThemPerDay()
    {
        // Arrange
        _booking.ClassSessions =
        [
            Session(At(1, 7, 0)) with { ClassTypeName = "Spinning", InstructorName = "Louise", FreeSeats = 12 },
            InOdense(Session(At(1, 17, 30)) with { ClassTypeName = "Yoga", InstructorName = "Henrik", FreeSeats = 3 }),
            Session(At(2, 9, 15)) with { ClassTypeName = "Power Circuit", InstructorName = "Amir", FreeSeats = 20 },
        ];

        // Act
        IRenderedComponent<ClassSchedule> cut = Render<ClassSchedule>();

        // Assert
        Assert.That(Joined(cut.FindAll("h2")), Is.EqualTo("01-12-2026 | 02-12-2026"));
        IReadOnlyList<IElement> tables = cut.FindAll("table");
        Assert.That(tables, Has.Count.EqualTo(2));
        Assert.That(Rows(tables[0]), Is.EqualTo(
            "07:00 | Spinning | FitLife Aarhus | Louise | 12" + Environment.NewLine +
            "17:30 | Yoga | FitLife Odense | Henrik | 3"));
        Assert.That(Rows(tables[1]), Is.EqualTo("09:15 | Power Circuit | FitLife Aarhus | Amir | 20"));
    }

    [Test]
    public void ClassSchedule_ShowsTheTimeAsBookingSendsIt()
    {
        // Arrange: no time zone conversion (Alpine has no culture data, and the time zone is not decided)
        _booking.ClassSessions = [Session(new DateTimeOffset(2026, 12, 1, 23, 45, 0, TimeSpan.Zero))];

        // Act
        IRenderedComponent<ClassSchedule> cut = Render<ClassSchedule>();

        // Assert
        Assert.That(Text(cut.Find("h2")), Is.EqualTo("01-12-2026"));
        Assert.That(Text(cut.Find("td")), Is.EqualTo("23:45"));
    }

    [Test]
    public void ClassSchedule_WhenAClassSessionIsFull_ShowsFuldtHold()
    {
        // Arrange
        _booking.ClassSessions = [Session(At(1, 7, 0)) with { FreeSeats = 0 }];

        // Act
        IRenderedComponent<ClassSchedule> cut = Render<ClassSchedule>();

        // Assert
        Assert.That(Text(cut.FindAll("td")[4]), Is.EqualTo("Fuldt hold"));
    }

    [Test]
    public void ClassSchedule_WhenAClassSessionIsCancelled_ShowsAflyst()
    {
        // Arrange: Booking sends 0 free seats for a cancelled class session
        _booking.ClassSessions = [Session(At(1, 7, 0)) with { FreeSeats = 0, IsCancelled = true }];

        // Act
        IRenderedComponent<ClassSchedule> cut = Render<ClassSchedule>();

        // Assert
        Assert.That(Text(cut.FindAll("td")[4]), Is.EqualTo("Aflyst"));
    }

    [Test]
    public void ClassSchedule_WithNoClassSessions_ShowsTheEmptySentence()
    {
        // Arrange
        _booking.ClassSessions = [];

        // Act
        IRenderedComponent<ClassSchedule> cut = Render<ClassSchedule>();

        // Assert
        Assert.That(Text(cut.Find(".empty-schedule")), Is.EqualTo("Der er ingen hold at vise."));
        Assert.That(cut.FindAll("table"), Is.Empty);
    }

    [Test]
    public void ClassSchedule_TheFilter_ListsAlleCentreAndEachCentreInTheAnswer()
    {
        // Arrange: Aarhus twice, so it must be listed once
        _booking.ClassSessions =
        [
            Session(At(1, 7, 0)),
            InOdense(Session(At(1, 8, 0))),
            Session(At(1, 9, 0)),
        ];

        // Act
        IRenderedComponent<ClassSchedule> cut = Render<ClassSchedule>();

        // Assert
        IElement form = cut.Find("form");
        Assert.That(form.GetAttribute("method"), Is.EqualTo("get"));
        Assert.That(Text(cut.Find("label[for=centerId]")), Is.EqualTo("Center"));
        Assert.That(cut.Find("select#centerId").GetAttribute("name"), Is.EqualTo("centerId"));
        Assert.That(Joined(cut.FindAll("select#centerId option")), Is.EqualTo("Alle centre | FitLife Aarhus | FitLife Odense"));
        Assert.That(string.Join(" | ", cut.FindAll("select#centerId option").Select(option => option.GetAttribute("value"))), Is.EqualTo($" | {Aarhus} | {Odense}"));
        Assert.That(Text(cut.Find("form button[type=submit]")), Is.EqualTo("Vis"));
    }

    [Test]
    public void ClassSchedule_WithACenterChosen_ShowsOnlyThatCentersClassSessions()
    {
        // Arrange
        _booking.ClassSessions =
        [
            Session(At(1, 7, 0)) with { ClassTypeName = "Spinning" },
            InOdense(Session(At(1, 8, 0)) with { ClassTypeName = "Yoga" }),
            Session(At(2, 9, 0)) with { ClassTypeName = "Pilates" },
        ];
        Services.GetRequiredService<NavigationManager>().NavigateTo($"hold?centerId={Odense}");

        // Act
        IRenderedComponent<ClassSchedule> cut = Render<ClassSchedule>();

        // Assert: only Odense's class session, and Odense chosen in the list, which still lists every centre
        Assert.That(Rows(cut.Find("table")), Is.EqualTo("08:00 | Yoga | FitLife Odense | Louise | 5"));
        Assert.That(Joined(cut.FindAll("h2")), Is.EqualTo("01-12-2026"));
        Assert.That(cut.Find($"option[value='{Odense}']").HasAttribute("selected"), Is.True);
        Assert.That(cut.Find($"option[value='{Aarhus}']").HasAttribute("selected"), Is.False);
    }

    [Test]
    public void ClassSchedule_WithACenterThatHasNoClassSessions_ShowsTheEmptySentence()
    {
        // Arrange
        _booking.ClassSessions = [Session(At(1, 7, 0))];
        Services.GetRequiredService<NavigationManager>().NavigateTo($"hold?centerId={Odense}");

        // Act
        IRenderedComponent<ClassSchedule> cut = Render<ClassSchedule>();

        // Assert
        Assert.That(Text(cut.Find(".empty-schedule")), Is.EqualTo("Der er ingen hold at vise."));
    }

    [Test]
    public void ClassSchedule_WhenTheCallFails_ShowsTheSentence()
    {
        // Arrange
        _booking.Failure = new BookingUnavailableException("Booking answered 500: System.InvalidOperationException at BookingService");

        // Act
        IRenderedComponent<ClassSchedule> cut = Render<ClassSchedule>();

        // Assert: the sentence and nothing from the error
        Assert.That(Text(cut.Find("[role=alert]")), Is.EqualTo("Vi kan ikke hente holdene lige nu. Prøv igen om lidt."));
        Assert.That(cut.Markup, Does.Not.Contain("500").And.Not.Contain("Exception"));
        Assert.That(cut.FindAll("table"), Is.Empty);
        Assert.That(cut.FindAll(".empty-schedule"), Is.Empty);
    }

    [Test]
    public void ClassSchedule_AsksBookingOnce()
    {
        // Act
        Render<ClassSchedule>();

        // Assert
        Assert.That(_booking.Calls, Is.EqualTo(1));
    }

    // A start in December 2026, at Danish winter time
    private static DateTimeOffset At(int day, int hour, int minute)
    {
        return new DateTimeOffset(2026, 12, day, hour, minute, 0, TimeSpan.FromHours(1));
    }

    private static ClassSessionResponse Session(DateTimeOffset startTime)
    {
        return new ClassSessionResponse(
            Guid.NewGuid(), Aarhus, "FitLife Aarhus", Guid.NewGuid(), "Spinning", "Louise", startTime, 60, 20, FreeSeats: 5, IsCancelled: false);
    }

    private static ClassSessionResponse InOdense(ClassSessionResponse session)
    {
        return session with { CenterId = Odense, CenterName = "FitLife Odense" };
    }

    // Each row's cells, " | " between cells and a new line between rows
    private static string Rows(IElement table)
    {
        return string.Join(Environment.NewLine, table.QuerySelectorAll("tbody tr").Select(row => Joined(row.QuerySelectorAll("td"))));
    }

    private static string Joined(IEnumerable<IElement> elements)
    {
        return string.Join(" | ", elements.Select(Text));
    }

    private static string Text(IElement element)
    {
        return element.TextContent.Trim();
    }
}
