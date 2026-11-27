using System.Collections.Concurrent;
using System.Net;
using System.Text;
using BookingService.Application;
using BookingService.Clients;
using FitLife.Contracts.Testing;
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

    // Every answer the stub gave in the current test, so the fake can't drift from Membership's contract (#522)
    private static readonly ConcurrentQueue<FakeAnswer> Served = new();

    [TearDown]
    public void TheFakesAnswers_AreInMembershipsContract()
    {
        IReadOnlyList<string> missing = OpenApiContracts.Missing("membershipservice", Served);
        Served.Clear();
        Assert.That(missing, Is.Empty, "The stub answers with something contracts/openapi/membershipservice.json doesn't have.");
    }

    [Test]
    public async Task FindMemberAsync_WhenMembershipAnswers200_ReturnsTheMemberIdAndWhetherItIsActive()
    {
        // Arrange
        using var membership = new StubMembership(HttpStatusCode.OK);
        using ServiceProvider services = Services(membership);

        // Act
        MemberLookup result = await Find(services);

        // Assert
        Assert.That(result, Is.EqualTo(new MemberLookup(MemberLookupOutcome.Found, MemberId, IsActive: true)));
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
        Assert.That(result, Is.EqualTo(new MemberLookup(MemberLookupOutcome.NotAMember, Guid.Empty, IsActive: false)));
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
    public async Task FindMemberAsync_WhenTheAnswerCannotBeRead_ReturnsUnavailable()
    {
        // Arrange
        using var membership = new StubMembership(HttpStatusCode.OK) { Body = "not json" };
        using ServiceProvider services = Services(membership);

        // Act
        MemberLookup result = await Find(services);

        // Assert
        Assert.That(result, Is.EqualTo(new MemberLookup(MemberLookupOutcome.Unavailable, Guid.Empty, IsActive: false)));
    }

    [Test]
    public async Task FindMemberAsync_WhenTheAnswerIsEmpty_ReturnsUnavailable()
    {
        // Arrange
        using var membership = new StubMembership(HttpStatusCode.OK) { Body = "null" };
        using ServiceProvider services = Services(membership);

        // Act
        MemberLookup result = await Find(services);

        // Assert
        Assert.That(result, Is.EqualTo(MemberLookup.Unavailable));
    }

    [Test]
    public async Task FindMemberAsync_WhenTheCircuitIsOpen_ReturnsUnavailableWithoutAskingMembership()
    {
        // Arrange: two failed attempts open the circuit
        using var membership = new StubMembership(HttpStatusCode.InternalServerError);
        using ServiceProvider services = Services(membership, configure: options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.FailureRatio = 0.5;
        });
        await Find(services);
        int askedBefore = membership.Requests;

        // Act
        MemberLookup result = await Find(services);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(MemberLookup.Unavailable));
            Assert.That(membership.Requests, Is.EqualTo(askedBefore));
        });
    }

    [Test]
    public void AddMembershipClient_WithItsDefaults_GivesTheMembershipClient()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [MembershipClient.BaseUrlConfigurationKey] = "https://membership.test" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddMembershipClient();
        using ServiceProvider provider = services.BuildServiceProvider();

        // Act
        IMembershipClient client = provider.GetRequiredService<IMembershipClient>();

        // Assert
        Assert.That(client, Is.InstanceOf<MembershipClient>());
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

    private static ServiceProvider Services(
        StubMembership membership, string? baseUrl = "https://membership.test", Action<HttpStandardResilienceOptions>? configure = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [MembershipClient.BaseUrlConfigurationKey] = baseUrl })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddMembershipClient(options =>
        {
            ShortDelays(options);
            configure?.Invoke(options);
        });
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

        public string Body { get; init; } = $$"""{ "memberId": "{{MemberId}}", "membershipStatus": "Active", "isActive": true }""";

        public int Requests { get; private set; }

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
            Requests++;
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
            Served.Enqueue(new FakeAnswer(request.Method, request.RequestUri?.AbsolutePath ?? string.Empty, status));
            return new HttpResponseMessage(status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
        }
    }
}
