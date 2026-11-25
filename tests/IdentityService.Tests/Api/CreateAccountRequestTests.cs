using IdentityService.Api;

namespace IdentityService.Tests.Api;

/// <summary>MVC logs an action's arguments with ToString() (at Debug), so the request must not print its password (#234).</summary>
public sealed class CreateAccountRequestTests
{
    [Test]
    public void ToString_HasTheEmailButNotThePassword()
    {
        // Arrange
        var request = new CreateAccountRequest("anna@fitlife.example", "made-up-password-1");

        // Act
        string text = request.ToString();

        // Assert
        Assert.That(text, Does.Contain("anna@fitlife.example"));
        Assert.That(text, Does.Not.Contain("made-up-password-1"));
    }
}
