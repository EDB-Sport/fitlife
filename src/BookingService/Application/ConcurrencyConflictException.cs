namespace BookingService.Application;

/// <summary>A save was refused because the class session was saved by another request since it was read. Nothing was stored (#245).</summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ConcurrencyConflictException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What was refused.</param>
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What was refused.</param>
    /// <param name="innerException">The cause.</param>
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
