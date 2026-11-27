namespace MemberPortal.Web.Clients;

/// <summary>What Identity answers when the portal asks for an account.</summary>
public enum CreateAccountAnswer
{
    /// <summary>201: the account is made.</summary>
    Created,

    /// <summary>409 with the code <c>EmailAlreadyTaken</c>: another account has the e-mail address.</summary>
    EmailAlreadyTaken,

    /// <summary>400: the e-mail address isn't an address, or the password is shorter than 8 characters.</summary>
    Rejected,
}
