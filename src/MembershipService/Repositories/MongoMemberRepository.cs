using MembershipService.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace MembershipService.Repositories;

/// <summary>Members in the collection <c>members</c>, with a unique index on the account id.</summary>
public sealed class MongoMemberRepository : IMemberRepository
{
    private readonly IMongoCollection<MemberDocument> _members;
    private readonly Lock _indexLock = new();
    private Task? _index;

    /// <summary>Uses the collection <c>members</c> in the service's database.</summary>
    /// <param name="database">The service's database.</param>
    public MongoMemberRepository(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _members = database.GetCollection<MemberDocument>("members");
    }

    /// <inheritdoc />
    public async Task<Member?> FindByAccountIdAsync(string accountId, CancellationToken cancellationToken)
    {
        MemberDocument? document = await _members.Find(member => member.AccountId == accountId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?.ToMember();
    }

    /// <inheritdoc />
    public async Task<bool> TryAddAsync(Member member, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        await UniqueAccountIdIndexAsync().ConfigureAwait(false);
        try
        {
            await _members.InsertOneAsync(MemberDocument.From(member), options: null, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates the unique index on the account id once. A creation that failed (MongoDB not answering yet) is tried again on
    /// the next join, instead of failing every join until the service restarts.
    /// </summary>
    private Task UniqueAccountIdIndexAsync()
    {
        lock (_indexLock)
        {
            if (_index is null || _index.IsFaulted || _index.IsCanceled)
            {
                _index = _members.Indexes.CreateOneAsync(new CreateIndexModel<MemberDocument>(
                    Builders<MemberDocument>.IndexKeys.Ascending(member => member.AccountId),
                    new CreateIndexOptions { Unique = true }));
            }

            return _index;
        }
    }

    /// <summary>A member as stored.</summary>
    internal sealed class MemberDocument
    {
        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid Id { get; init; }

        public string AccountId { get; init; } = string.Empty;

        public static MemberDocument From(Member member)
        {
            return new MemberDocument { Id = member.Id, AccountId = member.AccountId };
        }

        public Member ToMember()
        {
            return Member.Restore(Id, AccountId);
        }
    }
}
