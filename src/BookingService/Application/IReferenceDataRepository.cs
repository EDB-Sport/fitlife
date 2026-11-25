using BookingService.Models;

namespace BookingService.Application;

/// <summary>Reads the centres and class types the class schedule names (METHODOLOGY §11.2 rule 8). Read-only: #255 writes them.</summary>
public interface IReferenceDataRepository
{
    /// <summary>Every centre.</summary>
    /// <param name="cancellationToken">Stops the query.</param>
    Task<IReadOnlyList<Center>> ListCentersAsync(CancellationToken cancellationToken);

    /// <summary>Every class type.</summary>
    /// <param name="cancellationToken">Stops the query.</param>
    Task<IReadOnlyList<ClassType>> ListClassTypesAsync(CancellationToken cancellationToken);
}
