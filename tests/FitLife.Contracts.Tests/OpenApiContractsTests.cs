using System.Net;
using FitLife.Contracts.Testing;

namespace FitLife.Contracts.Tests;

/// <summary>
/// A consumer's fake serves only what the provider's OpenAPI contract has (#522): the route, the method and the status code.
/// Checked against the committed <c>contracts/openapi/*.json</c>.
/// </summary>
public sealed class OpenApiContractsTests
{
    private const string Membership = "membershipservice";
    private const string Booking = "bookingservice";

    [TestCase("GET", "/members/by-account/3f2b8c1d-4e5a-4b6c-8d7e-9f0a1b2c3d4e", HttpStatusCode.OK)]
    [TestCase("GET", "/members/by-account/3f2b8c1d-4e5a-4b6c-8d7e-9f0a1b2c3d4e", HttpStatusCode.NotFound)]
    [TestCase("POST", "/members", HttpStatusCode.Created)]
    [TestCase("PUT", "/members/me/pause", HttpStatusCode.Conflict)]
    public void Missing_ForAnAnswerTheContractHas_IsEmpty(string method, string path, HttpStatusCode status)
    {
        // Arrange
        FakeAnswer[] answers = [new(new HttpMethod(method), path, status)];

        // Act
        IReadOnlyList<string> missing = OpenApiContracts.Missing(Membership, answers);

        // Assert
        Assert.That(missing, Is.Empty);
    }

    [TestCase("GET", "/members/by-account-id/3f2b8c1d", HttpStatusCode.OK, TestName = "Missing_ForAMadeUpRoute_NamesIt")]
    [TestCase("GET", "/members/by-account", HttpStatusCode.OK, TestName = "Missing_ForARouteWithoutItsParameter_NamesIt")]
    [TestCase("DELETE", "/members/by-account/3f2b8c1d", HttpStatusCode.OK, TestName = "Missing_ForAMethodTheRouteDoesntHave_NamesIt")]
    [TestCase("GET", "/members/by-account/3f2b8c1d", HttpStatusCode.Conflict, TestName = "Missing_ForAStatusTheRouteDoesntAnswer_NamesIt")]
    public void Missing_ForAnAnswerTheContractDoesntHave_NamesIt(string method, string path, HttpStatusCode status)
    {
        // Arrange
        FakeAnswer[] answers = [new(new HttpMethod(method), path, status)];

        // Act
        IReadOnlyList<string> missing = OpenApiContracts.Missing(Membership, answers);

        // Assert
        Assert.That(missing, Has.Count.EqualTo(1));
        Assert.That(missing[0], Does.Contain($"{method} {path}").And.Contain(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public void Missing_ForAServerFailure_IsEmpty(HttpStatusCode status)
    {
        // Arrange: a 5xx is a failure the client must survive, which no contract lists
        FakeAnswer[] answers = [new(HttpMethod.Get, "/members/by-account/3f2b8c1d", status)];

        // Act
        IReadOnlyList<string> missing = OpenApiContracts.Missing(Membership, answers);

        // Assert
        Assert.That(missing, Is.Empty);
    }

    [Test]
    public void Missing_ThroughTheGateway_ComparesThePathWithoutTheGatewayRoute()
    {
        // Arrange
        FakeAnswer[] answers = [new(HttpMethod.Get, "/api/booking/class-sessions", HttpStatusCode.OK)];

        // Act
        IReadOnlyList<string> missing = OpenApiContracts.Missing(Booking, answers, gatewayRoute: "api/booking", notTheProvidersAnswers: []);

        // Assert
        Assert.That(missing, Is.Empty);
    }

    [Test]
    public void Missing_ThroughTheGateway_StillNamesAMadeUpRoute()
    {
        // Arrange
        FakeAnswer[] answers = [new(HttpMethod.Get, "/api/booking/class-schedule", HttpStatusCode.OK)];

        // Act
        IReadOnlyList<string> missing = OpenApiContracts.Missing(Booking, answers, gatewayRoute: "api/booking", notTheProvidersAnswers: []);

        // Assert
        Assert.That(missing, Has.Count.EqualTo(1));
    }

    [Test]
    public void Missing_ForAStatusNamedAsNotTheProvidersAnswer_IsEmpty()
    {
        // Arrange: the gateway answers 404 when Booking's route is missing, so the 404 isn't Booking's
        FakeAnswer[] answers = [new(HttpMethod.Get, "/api/booking/class-sessions", HttpStatusCode.NotFound)];

        // Act
        IReadOnlyList<string> missing = OpenApiContracts.Missing(Booking, answers, gatewayRoute: "api/booking", notTheProvidersAnswers: [HttpStatusCode.NotFound]);

        // Assert
        Assert.That(missing, Is.Empty);
    }

    [Test]
    public void Missing_ForTheSameMissingAnswerTwice_NamesItOnce()
    {
        // Arrange
        FakeAnswer answer = new(HttpMethod.Get, "/members/no-such-route", HttpStatusCode.OK);

        // Act
        IReadOnlyList<string> missing = OpenApiContracts.Missing(Membership, [answer, answer]);

        // Assert
        Assert.That(missing, Has.Count.EqualTo(1));
    }

    [Test]
    public void Missing_ForAContractThatDoesntExist_Throws()
    {
        // Arrange
        FakeAnswer[] answers = [new(HttpMethod.Get, "/members", HttpStatusCode.OK)];

        // Act & Assert
        Assert.That(() => OpenApiContracts.Missing("no-such-service", answers), Throws.TypeOf<FileNotFoundException>());
    }
}
