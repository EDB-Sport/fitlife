using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IdentityService.Repositories;

/// <summary>How an <see cref="Domain.Account"/> is stored. Only the repository maps to and from it, so the model has no MongoDB type.</summary>
internal sealed class AccountDocument
{
    /// <summary>The account's id, stored as a standard UUID.</summary>
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>The e-mail address as it was given. The unique index compares it without letter case.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The hash of the password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>The glossary's names of the roles.</summary>
    public List<string> Roles { get; set; } = [];
}
