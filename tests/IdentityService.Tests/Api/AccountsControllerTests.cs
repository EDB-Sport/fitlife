using IdentityService.Api;
using IdentityService.Application;
using IdentityService.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace IdentityService.Tests.Api;

/// <summary>The controller only turns the handler's answer into a status code (#234).</summary>
public sealed class AccountsControllerTests
{
    [Test]
    public void PostAccount_WithoutARequest_Throws()
    {
        // Arrange
        var handler = new CreateAccountHandler(new Mock<IAccountRepository>().Object, new PasswordHasher<Account>(), NullLogger<CreateAccountHandler>.Instance);
        var controller = new AccountsController(handler);

        // Act & Assert
        // ! The test passes null on purpose, to check the guard.
        Assert.That(() => controller.PostAccount(null!, CancellationToken.None), Throws.ArgumentNullException);
    }
}
