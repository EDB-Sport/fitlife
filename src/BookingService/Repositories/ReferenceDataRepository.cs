using BookingService.Application;
using BookingService.Models;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace BookingService.Repositories;

/// <summary>Reads centres from the <c>centers</c> collection and class types from <c>classTypes</c>. It never writes.</summary>
public sealed class ReferenceDataRepository : IReferenceDataRepository
{
    private readonly IMongoCollection<NamedDocument> _centers;
    private readonly IMongoCollection<NamedDocument> _classTypes;

    /// <summary>Creates the repository on the service's own database.</summary>
    /// <param name="database">The Booking database.</param>
    public ReferenceDataRepository(IMongoDatabase database)
    {
        _centers = database.GetCollection<NamedDocument>("centers");
        _classTypes = database.GetCollection<NamedDocument>("classTypes");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Center>> ListCentersAsync(CancellationToken cancellationToken)
    {
        List<NamedDocument> documents = await _centers.Find(FilterDefinition<NamedDocument>.Empty).ToListAsync(cancellationToken).ConfigureAwait(false);
        return documents.ConvertAll(document => new Center(document.Id, document.Name));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClassType>> ListClassTypesAsync(CancellationToken cancellationToken)
    {
        List<NamedDocument> documents = await _classTypes.Find(FilterDefinition<NamedDocument>.Empty).ToListAsync(cancellationToken).ConfigureAwait(false);
        return documents.ConvertAll(document => new ClassType(document.Id, document.Name));
    }

    /// <summary>How a centre or a class type is stored: an id and a name.</summary>
    [BsonIgnoreExtraElements]
    internal sealed class NamedDocument
    {
        [BsonId]
        public Guid Id { get; init; }

        public string Name { get; init; } = "";
    }
}
