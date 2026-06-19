using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.GitAccessToken.Contracts;

public class GitAccessTokenDto : ModelBase<GitAccessTokenId, Guid>
{
    public UserId OwnerUserId { get; set; } = default!;

    public string Name { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
