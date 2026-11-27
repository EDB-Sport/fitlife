using System.Net;
using MemberPortal.Web.Clients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace MemberPortal.Web.IntegrationTests;

/// <summary><c>/mit-medlemskab</c> needs a log-in (#624), and "Betal nu" (#535) and "Sæt på pause" (#211) post through the real pipeline.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class MyMembershipTests : IDisposable
{
    private static readonly DateOnly StartDate = new(2026, 11, 3);

    private readonly PortalFactory _factory = new();
    private readonly HttpClient _client;

    public MyMembershipTests()
    {
        // HTTPS, because the cookie is Secure; no auto-redirect, so the tests see the redirects
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task MyMembership_WithoutLogIn_RedirectsToLogInd()
    {
        // Act
        using HttpResponseMessage response = await _client.GetAsync(new Uri("/mit-medlemskab", UriKind.Relative)).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(PortalForms.RedirectPath(_client, response), Is.EqualTo("/log-ind"));
    }

    [Test]
    public async Task MyMembership_BetalNu_PaysWithTheFormAndShowsTheAnswer()
    {
        // Arrange
        using HttpResponseMessage logIn = await LogInAsync().ConfigureAwait(false);
        string page = await _client.GetStringAsync(new Uri("/mit-medlemskab", UriKind.Relative)).ConfigureAwait(false);

        // Act: what the browser sends for the button, its hidden fields
        using var form = new FormUrlEncodedContent(PortalForms.HiddenFields(page));
        using HttpResponseMessage response = await _client.PostAsync(new Uri("/mit-medlemskab", UriKind.Relative), form).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_factory.Membership.PayCalls, Is.EqualTo(1));
        string answer = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        Assert.That(answer, Does.Contain("Din betaling er modtaget.").And.Contain("Aktiv").And.Not.Contain("Betal nu"));
    }

    [Test]
    public async Task MyMembership_SaetPaaPause_SendsTheTwoDaysAndShowsTheAnswer()
    {
        // Arrange
        _factory.Membership.Status = "Active";
        using HttpResponseMessage logIn = await LogInAsync().ConfigureAwait(false);
        string page = await _client.GetStringAsync(new Uri("/mit-medlemskab", UriKind.Relative)).ConfigureAwait(false);
        Dictionary<string, string> fields = PortalForms.HiddenFields(page);
        fields["_handler"] = "pause";
        fields["PauseFormValues.From"] = "2026-12-01";
        fields["PauseFormValues.To"] = "2026-12-31";

        // Act
        using var form = new FormUrlEncodedContent(fields);
        using HttpResponseMessage response = await _client.PostAsync(new Uri("/mit-medlemskab", UriKind.Relative), form).ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(_factory.Membership.PauseRequest, Is.EqualTo(new PauseRequest(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31))));
        string answer = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        Assert.That(answer, Does.Contain("Dit medlemskab er sat på pause.").And.Contain("Pause fra 01-12-2026 til 31-12-2026"));
    }

    private async Task<HttpResponseMessage> LogInAsync()
    {
        string page = await _client.GetStringAsync(new Uri("/log-ind", UriKind.Relative)).ConfigureAwait(false);
        Dictionary<string, string> fields = PortalForms.HiddenFields(page);
        fields["Form.Email"] = "medlem@fitlife.test";
        fields["Form.Password"] = "hemmelig-adgangskode";
        using var form = new FormUrlEncodedContent(fields);
        return await _client.PostAsync(new Uri("/log-ind", UriKind.Relative), form).ConfigureAwait(false);
    }

    private sealed class FakeIdentity : IIdentityClient
    {
        public Task<CreateAccountAnswer> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The membership page creates no account.");
        }

        public Task<TokenResponse?> LogInAsync(LogInRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult<TokenResponse?>(new TokenResponse("header.payload.signature", new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero)));
        }
    }

    /// <summary>A member with Betaling mangler (or <see cref="Status"/>), who becomes Aktiv when paying and Pause when pausing.</summary>
    private sealed class FakeMembership : IMembershipClient
    {
        public string Status { get; set; } = "PaymentMissing";

        public int PayCalls { get; private set; }

        public PauseRequest? PauseRequest { get; private set; }

        public Task<JoinMembershipAnswer> JoinAsync(string? accessToken, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The membership page doesn't join.");
        }

        public Task<MyMembershipResponse?> GetMyMembershipAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<MyMembershipResponse?>(Membership(Status));
        }

        public Task<PaymentAnswer> PayAsync(CancellationToken cancellationToken)
        {
            PayCalls++;
            return Task.FromResult(new PaymentAnswer(Membership("Active"), RejectionCode: null));
        }

        public Task<PauseAnswer> PauseAsync(PauseRequest request, CancellationToken cancellationToken)
        {
            PauseRequest = request;
            return Task.FromResult(new PauseAnswer(
                new MembershipPauseResponse(Guid.Empty, "Standard", "Paused", IsActive: false, StartDate, request.From, request.To), RejectionCode: null));
        }

        public Task<PauseAnswer> EndPauseAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The tests here end no pause.");
        }

        private static MyMembershipResponse Membership(string status)
        {
            return new MyMembershipResponse(Guid.Empty, "Standard", status, IsActive: false, StartDate);
        }
    }

    private sealed class PortalFactory : WebApplicationFactory<Program>
    {
        public FakeMembership Membership { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IIdentityClient>(new FakeIdentity());
                services.AddSingleton<IMembershipClient>(Membership);
            });
        }
    }
}
