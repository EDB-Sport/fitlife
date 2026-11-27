using BookingService.Application;
using BookingService.Models;
using MongoDB.Driver;

namespace BookingService.Repositories;

/// <summary>Writes centres to the <c>centers</c> collection and class types to <c>classTypes</c>, the ones <see cref="ReferenceDataRepository"/> reads.</summary>
public sealed class ReferenceDataWriter : IReferenceDataWriter
{
    private static readonly ReplaceOptions Upsert = new() { IsUpsert = true };

    private readonly IMongoCollection<ReferenceDataRepository.NamedDocument> _centers;
    private readonly IMongoCollection<ReferenceDataRepository.NamedDocument> _classTypes;

    /// <summary>Creates the writer on the service's own database.</summary>
    /// <param name="database">The Booking database.</param>
    public ReferenceDataWriter(IMongoDatabase database)
    {
        _centers = database.GetCollection<ReferenceDataRepository.NamedDocument>("centers");
        _classTypes = database.GetCollection<ReferenceDataRepository.NamedDocument>("classTypes");
    }

    /// <inheritdoc />
    public async Task SaveCenterAsync(Center center, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(center);
        await _centers.ReplaceOneAsync(document => document.Id == center.Id, new ReferenceDataRepository.NamedDocument { Id = center.Id, Name = center.Name }, Upsert, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveClassTypeAsync(ClassType classType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(classType);
        await _classTypes.ReplaceOneAsync(document => document.Id == classType.Id, new ReferenceDataRepository.NamedDocument { Id = classType.Id, Name = classType.Name }, Upsert, cancellationToken)
            .ConfigureAwait(false);
    }
}
