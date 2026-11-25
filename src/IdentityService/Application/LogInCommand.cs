using System.Text;

namespace IdentityService.Application;

/// <summary>A person logs in with the e-mail address and the password of their account (Konto), and gets a token.</summary>
/// <param name="Email">The e-mail address of the account, in any letter case.</param>
/// <param name="Password">The password. It is only checked against the stored hash, and never logged.</param>
public sealed record LogInCommand(string Email, string Password)
{
    /// <summary>Prints the e-mail address only. MVC logs an action's arguments with ToString(), and the password is never logged.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Email = ").Append(Email);
        return true;
    }
}
