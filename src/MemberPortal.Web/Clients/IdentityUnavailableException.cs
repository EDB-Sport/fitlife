namespace MemberPortal.Web.Clients;

/// <summary>Identity answered an error or an answer that can't be read, or didn't answer at all.</summary>
public sealed class IdentityUnavailableException : Exception
{
    /// <summary>Creates the exception.</summary>
    public IdentityUnavailableException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong. Never shown to the person logging in.</param>
    public IdentityUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong. Never shown to the person logging in.</param>
    /// <param name="innerException">The cause.</param>
    public IdentityUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
