using System.Net;
using MemberPortal.Web;
using MemberPortal.Web.Clients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace MemberPortal.Web.IntegrationTests;

/// <summary>
/// Becoming a member at <c>/bliv-medlem</c> (#209): an account in Identity, a log-in, and joining in Membership, through
/// the real pipeline with fake clients that write down the calls in order.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)] // a new portal and cookie jar per test
public sealed class BecomeMemberTests : IDisposable
{
    private const string Password = "hemmelig-adgangskode";
    private static readonly string[] TheThreeCallsInOrder = ["POST api/identity/accounts", "POST api/identity/tokens", "POST api/membership/members"];
    private static readonly DateTimeOffset ExpiresAt = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PortalFactory _factory = new();
    private readonly HttpClient _client;

    public BecomeMemberTests()
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
    public async Task BecomeMember_WithANewEmail_CreatesTheAccountJoinsAndRedirectsToTheFrontPageLoggedIn()
    {
        // Act
        using HttpResponseMessage response = await BecomeMemberAsync().ConfigureAwait(false);

        // Assert
        Assert.That(_factory.Calls, Is.EqualTo(TheThreeCallsInOrder));
        Assert.That(_factory.Membership.AccessToken, Is.EqualTo(FakeIdentity.AccessToken), "The join didn't get the fresh token.");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(PortalForms.RedirectPath(_client, response), Is.EqualTo("/"));
        Assert.That(PortalForms.SetCookie(response, PortalForms.AuthenticationCookie), Is.Not.Null);
        string page = await _client.GetStringAsync(new Uri("/", UriKind.Relative)).ConfigureAwait(false);
        Assert.That(page, Does.Contain("medlem@fitlife.test"));
    }

    [Test]
    public async Task BecomeMember_WhenJoiningFails_LeavesThePersonLoggedInWithTheSentence()
    {
        // Arrange
        _factory.Membership.Failure = new MembershipUnavailableException("Membership is not reachable.");

        // Act
        using HttpResponseMessage response = await BecomeMemberAsync().ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(PortalForms.SetCookie(response, PortalForms.AuthenticationCookie), Is.Not.Null);
        // Razor encodes å and ø, so the test reads the page as a browser shows it
        string page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        Assert.That(page, Does.Contain("Din konto er oprettet, men vi kunne ikke melde dig ind. Prøv igen."));
        Assert.That(page, Does.Not.Contain("id=\"password\""), "The page still asks for the account it just made.");
    }

    [Test]
    public async Task BecomeMember_WhenTheFirstFeeIsNotPaid_StaysOnThePageLoggedInWithTheSentence()
    {
        // Arrange
        _factory.Membership.Answer = JoinMembershipAnswer.PaymentMissing;

        // Act
        using HttpResponseMessage response = await BecomeMemberAsync().ConfigureAwait(false);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(PortalForms.SetCookie(response, PortalForms.AuthenticationCookie), Is.Not.Null);
        // Razor encodes å and ø, so the test reads the page as a browser shows it
        string page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        Assert.That(page, Does.Contain("Du er meldt ind, men betalingen mangler. Du kan ikke booke, før den er betalt."));
        Assert.That(page, Does.Not.Contain("<form"));
    }

    [Test]
    public async Task BecomeMember_WhenItFails_NeverWritesThePasswordBackIntoThePage()
    {
        // Arrange
        _factory.Identity.CreateAccountAnswer = CreateAccountAnswer.Rejected;

        // Act
        using HttpResponseMessage response = await BecomeMemberAsync().ConfigureAwait(false);

        // Assert
        // Razor encodes å and ø, so the test reads the page as a browser shows it
        string page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        Assert.That(page, Does.Contain("Skriv en gyldig e-mail og en adgangskode på mindst 8 tegn."));
        Assert.That(page, Does.Not.Contain(Password));
    }

    [Test]
    public async Task BecomeMember_WithoutAName_ShowsTheSentenceAndNeverWritesThePasswordBackIntoThePage()
    {
        // Act
        using HttpResponseMessage response = await BecomeMemberAsync(name: string.Empty).ConfigureAwait(false);

        // Assert
        // Razor encodes å and ø, so the test reads the page as a browser shows it
        string page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        Assert.That(page, Does.Contain("Skriv dit navn."));
        Assert.That(page, Does.Not.Contain(Password));
    }

    [Test]
    public async Task BecomeMember_WithAnEmailThatIsTaken_NeverWritesThePasswordBackIntoThePage()
    {
        // Arrange
        _factory.Identity.CreateAccountAnswer = CreateAccountAnswer.EmailAlreadyTaken;

        // Act
        using HttpResponseMessage response = await BecomeMemberAsync().ConfigureAwait(false);

        // Assert
        string page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        Assert.That(page, Does.Contain(ErrorSentences.EmailAlreadyTaken));
        Assert.That(page, Does.Not.Contain(Password));
    }

    private async Task<HttpResponseMessage> BecomeMemberAsync(string name = "Mette Medlem")
    {
        string page = await _client.GetStringAsync(new Uri("/bliv-medlem", UriKind.Relative)).ConfigureAwait(false);
        Dictionary<string, string> fields = PortalForms.HiddenFields(page);
        fields["Form.Name"] = name;
        fields["Form.Email"] = "medlem@fitlife.test";
        fields["Form.Password"] = Password;
        using var form = new FormUrlEncodedContent(fields);
        return await _client.PostAsync(new Uri("/bliv-medlem", UriKind.Relative), form).ConfigureAwait(false);
    }

    private sealed class FakeIdentity(List<string> calls) : IIdentityClient
    {
        public const string AccessToken = "header.payload.signature-for-the-test";

        public CreateAccountAnswer CreateAccountAnswer { get; set; } = CreateAccountAnswer.Created;

        public Task<CreateAccountAnswer> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken)
        {
            calls.Add("POST api/identity/accounts");
            return Task.FromResult(CreateAccountAnswer);
        }

        public Task<TokenResponse?> LogInAsync(LogInRequest request, CancellationToken cancellationToken)
        {
            calls.Add("POST api/identity/tokens");
            return Task.FromResult<TokenResponse?>(new TokenResponse(AccessToken, ExpiresAt));
        }
    }

    private sealed class FakeMembership(List<string> calls) : IMembershipClient
    {
        public Exception? Failure { get; set; }

        public JoinMembershipAnswer Answer { get; set; } = JoinMembershipAnswer.Joined;

        public string? AccessToken { get; private set; }

        public Task<JoinMembershipAnswer> JoinAsync(string? accessToken, CancellationToken cancellationToken)
        {
            calls.Add("POST api/membership/members");
            AccessToken = accessToken;
            if (Failure is not null)
            {
                return Task.FromException<JoinMembershipAnswer>(Failure);
            }

            return Task.FromResult(Answer);
        }

        public Task<MyMembershipResponse?> GetMyMembershipAsync(CancellationToken cancellationToken)
        {
            calls.Add("GET api/membership/members/me");
            return Task.FromResult<MyMembershipResponse?>(null);
        }

        public Task<PaymentAnswer> PayAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Becoming a member pays nothing.");
        }
    }

    private sealed class PortalFactory : WebApplicationFactory<Program>
    {
        public PortalFactory()
        {
            Identity = new FakeIdentity(Calls);
            Membership = new FakeMembership(Calls);
        }

        public List<string> Calls { get; } = [];

        public FakeIdentity Identity { get; }

        public FakeMembership Membership { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IIdentityClient>(Identity);
                services.AddSingleton<IMembershipClient>(Membership);
            });
        }
    }
}
