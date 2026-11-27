using MemberPortal.Web.Clients;

namespace MemberPortal.Web.Tests;

/// <summary>
/// Identity in front of the portal's pages: answers one token, no token (401), or throws, answers one
/// <see cref="Clients.CreateAccountAnswer"/>, and remembers the requests.
/// </summary>
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

    public CreateAccountAnswer CreateAccountAnswer { get; set; } = CreateAccountAnswer.Created;

    public CreateAccountRequest? CreateAccountRequest { get; private set; }

    public Task<CreateAccountAnswer> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        CreateAccountRequest = request;
        return Task.FromResult(CreateAccountAnswer);
    }

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
