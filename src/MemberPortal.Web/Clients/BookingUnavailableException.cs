namespace MemberPortal.Web.Clients;

/// <summary>Booking answered an error, or didn't answer at all.</summary>
public sealed class BookingUnavailableException : Exception
{
    /// <summary>Creates the exception.</summary>
    public BookingUnavailableException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong. Never shown to the person.</param>
    public BookingUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong. Never shown to the person.</param>
    /// <param name="innerException">The cause.</param>
    public BookingUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
