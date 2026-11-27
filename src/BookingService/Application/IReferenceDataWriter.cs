using BookingService.Models;

namespace BookingService.Application;

/// <summary>Writes the centres and class types of the demo data (#255). Only the seeder uses it; the schedule reads through <see cref="IReferenceDataRepository"/>.</summary>
public interface IReferenceDataWriter
{
    /// <summary>Stores the centre, or replaces the one with the same id.</summary>
    /// <param name="center">The centre.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    Task SaveCenterAsync(Center center, CancellationToken cancellationToken);

    /// <summary>Stores the class type, or replaces the one with the same id.</summary>
    /// <param name="classType">The class type.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    Task SaveClassTypeAsync(ClassType classType, CancellationToken cancellationToken);
}
