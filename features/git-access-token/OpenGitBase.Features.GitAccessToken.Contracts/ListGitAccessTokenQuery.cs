using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.GitAccessToken.Contracts;

public class ListGitAccessTokenQuery
    : ListOfModelQuery<GitAccessTokenDto, GitAccessTokenId, Guid, ListGitAccessTokenQuery>
{
    public Option<UserId> ForUser { get; set; } = Option<UserId>.None;
}
