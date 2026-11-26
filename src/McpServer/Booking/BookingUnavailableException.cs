namespace McpServer.Booking;

/// <summary>Booking answered an error or an answer that can't be read, or didn't answer at all.</summary>
public sealed class BookingUnavailableException : Exception
{
    /// <summary>Creates the exception.</summary>
    public BookingUnavailableException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong. Logged, never given to the AI assistant.</param>
    public BookingUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong. Logged, never given to the AI assistant.</param>
    /// <param name="innerException">The cause.</param>
    public BookingUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
