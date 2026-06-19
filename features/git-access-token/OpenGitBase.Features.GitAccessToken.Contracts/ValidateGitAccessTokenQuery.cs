using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.GitAccessToken.Contracts;

public class ValidateGitAccessTokenQuery
    : IQuery<ValidateGitAccessTokenResult, ValidateGitAccessTokenQuery>
{
    public string Token { get; set; } = string.Empty;
}
