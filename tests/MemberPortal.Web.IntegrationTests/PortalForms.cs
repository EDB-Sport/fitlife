using System.Net;
using System.Text.RegularExpressions;

namespace MemberPortal.Web.IntegrationTests;

/// <summary>What a browser does with the portal's static forms: send back the hidden fields, and read the cookie and the redirect.</summary>
internal static partial class PortalForms
{
    /// <summary>The name of the authentication cookie.</summary>
    public const string AuthenticationCookie = ".AspNetCore.Cookies";

    /// <summary>The hidden fields of a page, among them the antiforgery token.</summary>
    /// <param name="page">The page's HTML.</param>
    /// <returns>The fields by name.</returns>
    public static Dictionary<string, string> HiddenFields(string page)
    {
        // The antiforgery field can appear once per form; its value is the same in every form of one page
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in HiddenField().Matches(page))
        {
            fields.TryAdd(WebUtility.HtmlDecode(match.Groups["name"].Value), WebUtility.HtmlDecode(match.Groups["value"].Value));
        }

        return fields;
    }

    /// <summary>The <c>Set-Cookie</c> header of one cookie.</summary>
    /// <param name="response">The response.</param>
    /// <param name="name">The cookie's name.</param>
    /// <returns>The header, or <see langword="null"/> when the response sets no such cookie.</returns>
    public static string? SetCookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies))
        {
            return null;
        }

        return cookies.FirstOrDefault(cookie => cookie.StartsWith(name + "=", StringComparison.Ordinal));
    }

    /// <summary>The path a redirect goes to, whether its Location is relative or absolute.</summary>
    /// <param name="client">The client that got the response.</param>
    /// <param name="response">The response.</param>
    /// <returns>The path, or <see langword="null"/> when the response is no redirect.</returns>
    public static string? RedirectPath(HttpClient client, HttpResponseMessage response)
    {
        if (response.Headers.Location is not Uri location || client.BaseAddress is not Uri baseAddress)
        {
            return null;
        }

        return new Uri(baseAddress, location).AbsolutePath;
    }

    [GeneratedRegex("""<input type="hidden" name="(?<name>[^"]+)" value="(?<value>[^"]*)" */?>""", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HiddenField();
}
