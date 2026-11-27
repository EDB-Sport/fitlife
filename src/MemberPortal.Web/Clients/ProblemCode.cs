using System.Text.Json;

namespace MemberPortal.Web.Clients;

/// <summary>Reads the <c>code</c> a service puts in its problem details, which a client switches on instead of the text.</summary>
internal static class ProblemCode
{
    /// <summary>Whether a problem answer carries this <c>code</c>. An answer that can't be read has none.</summary>
    /// <param name="response">The service's answer.</param>
    /// <param name="code">The code to look for.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>True when the body is a JSON object whose <c>code</c> is exactly <paramref name="code"/>.</returns>
    public static async Task<bool> HasCode(HttpResponseMessage response, string code, CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument problem = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
            return problem.RootElement.ValueKind == JsonValueKind.Object
                && problem.RootElement.TryGetProperty("code", out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && string.Equals(value.GetString(), code, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
