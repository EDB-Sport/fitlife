namespace MemberPortal.Web.Clients;

/// <summary>Membership answered an error, or didn't answer at all.</summary>
public sealed class MembershipUnavailableException : Exception
{
    /// <summary>Creates the exception.</summary>
    public MembershipUnavailableException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong. Never shown to the person.</param>
    public MembershipUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong. Never shown to the person.</param>
    /// <param name="innerException">The cause.</param>
    public MembershipUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
