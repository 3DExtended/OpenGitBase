using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.GitAccessToken.Contracts;

namespace OpenGitBase.Features.GitAccessToken.Contracts;

public class DeleteGitAccessTokenQuery
    : DeleteCommand<GitAccessTokenDto, GitAccessTokenId, Guid, DeleteGitAccessTokenQuery>;
