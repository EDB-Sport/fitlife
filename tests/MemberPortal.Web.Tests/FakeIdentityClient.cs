using MemberPortal.Web.Clients;

namespace MemberPortal.Web.Tests;

/// <summary>Identity in front of the log-in page: answers one token, no token (401), or throws, and remembers the request.</summary>
internal sealed class FakeIdentityClient : IIdentityClient
{
    private readonly TokenResponse? _token;
    private readonly Exception? _failure;

    public FakeIdentityClient(TokenResponse? token)
    {
        _token = token;
    }

    public FakeIdentityClient(Exception failure)
    {
        _failure = failure;
    }

    public LogInRequest? Request { get; private set; }

    public Task<TokenResponse?> LogInAsync(LogInRequest request, CancellationToken cancellationToken)
    {
        Request = request;
        if (_failure is not null)
        {
            return Task.FromException<TokenResponse?>(_failure);
        }

        return Task.FromResult(_token);
    }
}
