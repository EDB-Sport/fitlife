using MembershipService.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace MembershipService.Repositories;

/// <summary>Memberships in the collection <c>memberships</c>.</summary>
/// <param name="database">The service's database.</param>
public sealed class MongoMembershipRepository(IMongoDatabase database) : IMembershipRepository
{
    private readonly IMongoCollection<MembershipDocument> _memberships = database.GetCollection<MembershipDocument>("memberships");

    /// <inheritdoc />
    public Task AddAsync(Membership membership, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(membership);
        return _memberships.InsertOneAsync(MembershipDocument.From(membership), options: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        return _memberships.DeleteOneAsync(membership => membership.Id == membershipId, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(Membership membership, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(membership);
        return _memberships.ReplaceOneAsync(stored => stored.Id == membership.Id, MembershipDocument.From(membership), cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Membership?> FindByMemberIdAsync(Guid memberId, CancellationToken cancellationToken)
    {
        MembershipDocument? document = await _memberships.Find(membership => membership.MemberId == memberId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?.ToMembership();
    }

    /// <summary>A membership as stored. The enums are stored by name, so a stored value doesn't change meaning when one is added.</summary>
    internal sealed class MembershipDocument
    {
        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid Id { get; init; }

        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid MemberId { get; init; }

        [BsonRepresentation(BsonType.String)]
        public MembershipType Type { get; init; }

        [BsonRepresentation(BsonType.String)]
        public MembershipStatus Status { get; init; }

        public DateOnly StartDate { get; init; }

        public static MembershipDocument From(Membership membership)
        {
            return new MembershipDocument
            {
                Id = membership.Id,
                MemberId = membership.MemberId,
                Type = membership.Type,
                Status = membership.Status,
                StartDate = membership.StartDate,
            };
        }

        public Membership ToMembership()
        {
            return Membership.Restore(Id, MemberId, Type, Status, StartDate);
        }
    }
}
