using System.Net;
using System.Text;
using BookingService.Application;
using BookingService.Clients;
using FitLife.Correlation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookingService.Tests.Clients;

/// <summary>
/// Booking asks Membership who the member behind an account is (#256, CONTEXT-MAP section 4). The real registration, with a
/// stub handler in place of Membership and shorter resilience delays, so no test waits seconds.
/// </summary>
public sealed class MembershipClientTests
{
    private const string BearerToken = "the-members-own-token";
    private static readonly Guid AccountId = Guid.Parse("3f2b8c1d-4e5a-4b6c-8d7e-9f0a1b2c3d4e");
    private static readonly Guid MemberId = Guid.Parse("7d0f6c1a-2b3c-4d5e-8f90-a1b2c3d4e5f6");

    [Test]
    public async Task FindMemberAsync_WhenMembershipAnswers200_ReturnsTheMemberIdAndWhetherItIsActive()
    {
        // Arrange
        using var membership = new StubMembership(HttpStatusCode.OK);
        using ServiceProvider services = Services(membership);

        // Act
        MemberLookup result = await Find(services);

        // Assert
        Assert.That(result, Is.EqualTo(MemberLookup.Found(MemberId, isActive: true)));
    }

    [Test]
    public async Task FindMemberAsync_WhenMembershipAnswers404_ReturnsNotAMember()
    {
        // Arrange
        using var membership = new StubMembership(HttpStatusCode.NotFound);
        using ServiceProvider services = Services(membership);

        // Act
        MemberLookup result = await Find(services);

        // Assert
        Assert.That(result, Is.EqualTo(MemberLookup.NotAMember));
    }

    [Test]
    public async Task FindMemberAsync_WhenTheFirstAnswerIs500_TriesAgainAndReturnsTheMember()
    {
        // Arrange
        using var membership = new StubMembership(HttpStatusCode.InternalServerError, HttpStatusCode.OK);
        using ServiceProvider services = Services(membership);

        // Act
        MemberLookup result = await Find(services);

        // Assert
        Assert.That(result, Is.EqualTo(MemberLookup.Found(MemberId, isActive: true)));
    }

    [Test]
    public async Task FindMemberAsync_WhenMembershipKeepsAnswering500_ReturnsUnavailable()
    {
        // Arrange
        using var membership = new StubMembership(HttpStatusCode.InternalServerError);
        using ServiceProvider services = Services(membership);

        // Act
        MemberLookup result = await Find(services);

        // Assert
        Assert.That(result, Is.EqualTo(MemberLookup.Unavailable));
    }

    [Test]
    public async Task FindMemberAsync_WhenMembershipDoesNotAnswerInTime_ReturnsUnavailable()
    {
        // Arrange
        using var membership = StubMembership.ThatNeverAnswers();
        using ServiceProvider services = Services(membership);

        // Act
        MemberLookup result = await Find(services);

        // Assert
        Assert.That(result, Is.EqualTo(MemberLookup.Unavailable));
    }

    [Test]
    public async Task FindMemberAsync_WhenMembershipCannotBeReached_ReturnsUnavailable()
    {
        // Arrange
        using var membership = StubMembership.ThatCannotBeReached();
        using ServiceProvider services = Services(membership);

        // Act
        MemberLookup result = await Find(services);

        // Assert
        Assert.That(result, Is.EqualTo(MemberLookup.Unavailable));
    }

    [Test]
    public async Task FindMemberAsync_SendsTheBearerTokenAndTheCorrelationId()
    {
        // Arrange
        using var membership = new StubMembership(HttpStatusCode.OK);
        using ServiceProvider services = Services(membership);

        // Act
        using (CorrelationContext.Begin(NullLogger.Instance, "correlation-1"))
        {
            await Find(services);
        }

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(membership.Path, Is.EqualTo($"/members/by-account/{AccountId}"));
            Assert.That(membership.Authorization, Is.EqualTo($"Bearer {BearerToken}"));
            Assert.That(membership.CorrelationId, Is.EqualTo("correlation-1"));
        });
    }

    [Test]
    public async Task FindMemberAsync_WithoutABaseUrl_ThrowsAndNamesTheKey()
    {
        // Arrange
        using var membership = new StubMembership(HttpStatusCode.OK);
        using ServiceProvider services = Services(membership, baseUrl: null);

        // Act
        InvalidOperationException? failure = null;
        try
        {
            await Find(services).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
        }

        // Assert
        Assert.That(failure?.Message, Does.Contain(MembershipClient.BaseUrlConfigurationKey));
    }

    private static Task<MemberLookup> Find(ServiceProvider services)
    {
        return services.GetRequiredService<IMembershipClient>().FindMemberAsync(AccountId, BearerToken, CancellationToken.None);
    }

    private static ServiceProvider Services(StubMembership membership, string? baseUrl = "https://membership.test")
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [MembershipClient.BaseUrlConfigurationKey] = baseUrl })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddMembershipClient(ShortDelays);
        services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => membership));
        return services.BuildServiceProvider();
    }

    /// <summary>The standard handler's rules with milliseconds instead of seconds.</summary>
    private static void ShortDelays(HttpStandardResilienceOptions options)
    {
        options.Retry.Delay = TimeSpan.Zero;
        options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(100);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMilliseconds(500);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(1);
    }

    /// <summary>Membership's lookup: answers with the given statuses in turn, the last one from then on.</summary>
    private sealed class StubMembership : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses;
        private readonly bool _neverAnswers;
        private readonly bool _cannotBeReached;

        public StubMembership(params HttpStatusCode[] statuses)
            : this(statuses, neverAnswers: false, cannotBeReached: false)
        {
        }

        private StubMembership(HttpStatusCode[] statuses, bool neverAnswers, bool cannotBeReached)
        {
            _statuses = new Queue<HttpStatusCode>(statuses);
            _neverAnswers = neverAnswers;
            _cannotBeReached = cannotBeReached;
        }

        public string? Path { get; private set; }

        public string? Authorization { get; private set; }

        public string? CorrelationId { get; private set; }

        public static StubMembership ThatNeverAnswers()
        {
            return new StubMembership([], neverAnswers: true, cannotBeReached: false);
        }

        public static StubMembership ThatCannotBeReached()
        {
            return new StubMembership([], neverAnswers: false, cannotBeReached: true);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();
            if (request.Headers.TryGetValues(CorrelationContext.HeaderName, out IEnumerable<string>? ids))
            {
                CorrelationId = ids.Single();
            }

            if (_cannotBeReached)
            {
                throw new HttpRequestException("No connection could be made to Membership.");
            }

            if (_neverAnswers)
            {
                // Waits until the resilience handler's time-out cancels the request
                var answer = new TaskCompletionSource<HttpResponseMessage>();
                using CancellationTokenRegistration registration = cancellationToken.Register(() => answer.TrySetCanceled(cancellationToken));
                return await answer.Task.ConfigureAwait(false);
            }

            HttpStatusCode status = _statuses.Peek();
            if (_statuses.Count > 1)
            {
                _statuses.Dequeue();
            }
            string body = $$"""{ "memberId": "{{MemberId}}", "membershipStatus": "Active", "isActive": true }""";
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
