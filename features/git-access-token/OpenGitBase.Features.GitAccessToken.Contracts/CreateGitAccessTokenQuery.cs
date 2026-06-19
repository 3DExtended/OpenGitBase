using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.GitAccessToken.Contracts;

public class CreateGitAccessTokenQuery
    : IQuery<CreateGitAccessTokenResult, CreateGitAccessTokenQuery>
{
    public UserId OwnerUserId { get; set; } = default!;

    public string Name { get; set; } = string.Empty;

    public string Scope { get; set; } = GitAccessTokenScopes.Read;

    public DateTimeOffset? ExpiresAt { get; set; }

    public bool NeverExpires { get; set; }
}
