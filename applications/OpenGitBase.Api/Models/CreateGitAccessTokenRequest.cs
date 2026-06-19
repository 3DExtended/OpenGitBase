using OpenGitBase.Features.GitAccessToken.Contracts;

namespace OpenGitBase.Api.Models;

public sealed class CreateGitAccessTokenRequest
{
    public string Name { get; init; } = string.Empty;

    public string Scope { get; init; } = GitAccessTokenScopes.Read;

    public DateTimeOffset? ExpiresAt { get; init; }

    public bool NeverExpires { get; init; }
}
