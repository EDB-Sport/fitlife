using System.Net;

namespace FitLife.Contracts.Testing;

/// <summary>One answer a consumer's fake of a provider gave: the request's method and path, and the status it answered (#522).</summary>
/// <param name="Method">The request's method.</param>
/// <param name="Path">The request's path, without the query string, as <c>Uri.AbsolutePath</c> gives it.</param>
/// <param name="Status">The status the fake answered with.</param>
public sealed record FakeAnswer(HttpMethod Method, string Path, HttpStatusCode Status);
