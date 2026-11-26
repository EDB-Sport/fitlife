using System.ComponentModel.DataAnnotations;
using System.Text;

namespace IdentityService.Api;

/// <summary>A person logs in.</summary>
/// <param name="Email">The e-mail address of the account, in any letter case.</param>
/// <param name="Password">The password of the account.</param>
public sealed record LogInRequest(
    [Required] string Email,
    [Required] string Password)
{
    /// <summary>Prints the e-mail address only. MVC logs an action's arguments with ToString(), and the password is never logged.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Email = ").Append(Email);
        return true;
    }
}
