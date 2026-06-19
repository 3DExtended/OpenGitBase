using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.GitAccessToken.Contracts;

namespace OpenGitBase.Features.GitAccessToken.Contracts;

public class GetGitAccessTokenQuery
    : SingleModelQuery<GitAccessTokenDto, GitAccessTokenId, Guid, GetGitAccessTokenQuery>;
