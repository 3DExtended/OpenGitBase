using System.ComponentModel.DataAnnotations.Schema;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.GitAccessToken.Entities;

public class GitAccessTokenEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    [ForeignKey(nameof(OwnerUser))]
    public Guid OwnerUserId { get; set; }

    public UserEntity? OwnerUser { get; set; }

    public string Name { get; set; } = string.Empty;

    public string TokenLookupHash { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
