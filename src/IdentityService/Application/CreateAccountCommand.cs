using System.Text;

namespace IdentityService.Application;

/// <summary>A person creates an account (Konto) with an e-mail address and a password: the first of the two sign-up steps.</summary>
/// <param name="Email">The e-mail address the person will log in with.</param>
/// <param name="Password">The password. Only its hash is stored, and it is never logged.</param>
public sealed record CreateAccountCommand(string Email, string Password)
{
    /// <summary>Prints the e-mail address only. MVC logs an action's arguments with ToString(), and the password is never logged.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Email = ").Append(Email);
        return true;
    }
}
