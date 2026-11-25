using IdentityService.Application;

namespace IdentityService.Tests.Application;

/// <summary>The password is never logged (TEAM003), also not through the command's ToString() (#234).</summary>
public sealed class CreateAccountCommandTests
{
    [Test]
    public void ToString_HasTheEmailButNotThePassword()
    {
        // Arrange
        var command = new CreateAccountCommand("anna@fitlife.example", "made-up-password-1");

        // Act
        string text = command.ToString();

        // Assert
        Assert.That(text, Is.EqualTo("CreateAccountCommand { Email = anna@fitlife.example }"));
    }
}
