using System.ComponentModel.DataAnnotations;
using System.Text;

namespace IdentityService.Api;

/// <summary>A person asks for an account.</summary>
/// <param name="Email">The e-mail address to log in with.</param>
/// <param name="Password">At least 8 characters. Only its hash is stored.</param>
public sealed record CreateAccountRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password)
{
    /// <summary>Prints the e-mail address only. MVC logs an action's arguments with ToString(), and the password is never logged.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Email = ").Append(Email);
        return true;
    }
}
