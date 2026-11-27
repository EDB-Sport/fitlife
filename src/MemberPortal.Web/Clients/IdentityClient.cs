using System.Net;
using System.Text.Json;

namespace MemberPortal.Web.Clients;

/// <summary>
/// Identity's <c>POST accounts</c> and <c>POST tokens</c>, reached through the gateway's route <c>api/identity</c>, over a typed client.
/// A 401 is no token, and a 400 or a 409 <c>EmailAlreadyTaken</c> is an answer about the account; every other failure becomes an <see cref="IdentityUnavailableException"/>.
/// </summary>
/// <param name="http">A client with the gateway's base address, from <c>IHttpClientFactory</c>.</param>
public sealed class IdentityClient(HttpClient http) : IIdentityClient
{
    /// <summary>The configuration key of the gateway's address.</summary>
    public const string BaseUrlConfigurationKey = "Gateway:BaseUrl";

    private const string TokensPath = "api/identity/tokens";

    private const string AccountsPath = "api/identity/accounts";

    private const string EmailAlreadyTakenCode = "EmailAlreadyTaken";

    /// <inheritdoc />
    public async Task<CreateAccountAnswer> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await http.PostAsJsonAsync(new Uri(AccountsPath, UriKind.Relative), request, cancellationToken).ConfigureAwait(false);
            switch (response.StatusCode)
            {
                case HttpStatusCode.BadRequest:
                    return CreateAccountAnswer.Rejected;
                case HttpStatusCode.Conflict when await ProblemCode.HasCode(response, EmailAlreadyTakenCode, cancellationToken).ConfigureAwait(false):
                    return CreateAccountAnswer.EmailAlreadyTaken;
                default:
                    response.EnsureSuccessStatusCode();
                    return CreateAccountAnswer.Created;
            }
        }
        catch (HttpRequestException exception)
        {
            throw new IdentityUnavailableException("Identity can't be reached, or answered an error.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IdentityUnavailableException("Identity didn't answer in time.", exception);
        }
        catch (IOException exception)
        {
            throw new IdentityUnavailableException("The connection to Identity ended before the answer was read.", exception);
        }
    }

    /// <inheritdoc />
    public async Task<TokenResponse?> LogInAsync(LogInRequest request, CancellationToken cancellationToken)
    {
        TokenResponse? token;
        try
        {
            using HttpResponseMessage response = await http.PostAsJsonAsync(new Uri(TokensPath, UriKind.Relative), request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            // No connection, or an error status other than 401
            throw new IdentityUnavailableException("Identity can't be reached, or answered an error.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's timeout. The caller's own cancellation passes on untouched
            throw new IdentityUnavailableException("Identity didn't answer in time.", exception);
        }
        catch (IOException exception)
        {
            // The connection ended in the middle of the body (HttpIOException is an IOException)
            throw new IdentityUnavailableException("The connection to Identity ended before the answer was read.", exception);
        }
        catch (JsonException exception)
        {
            throw new IdentityUnavailableException("Identity's answer can't be read.", exception);
        }

        return token ?? throw new IdentityUnavailableException("Identity's answer is empty.");
    }
}
