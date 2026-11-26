using IdentityService.IntegrationTests.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IdentityService.IntegrationTests;

/// <summary>
/// The app with the test signing key (Jwt:SigningKey): Identity checks tokens (#364), so it needs a key to start.
/// Nothing else is replaced, and nothing connects to MongoDB before a request needs it, so /health answers without one.
/// </summary>
internal sealed class TestKeyFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", IdentityApiFactory.SigningKey);
    }
}
