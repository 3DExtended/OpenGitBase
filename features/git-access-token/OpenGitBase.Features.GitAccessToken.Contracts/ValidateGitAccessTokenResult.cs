using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.GitAccessToken.Contracts;

public sealed class ValidateGitAccessTokenResult
{
    public UserId UserId { get; set; } = default!;

    public string Scope { get; set; } = string.Empty;
}
