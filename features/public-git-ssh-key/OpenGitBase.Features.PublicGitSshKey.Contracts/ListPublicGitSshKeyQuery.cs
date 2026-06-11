using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.PublicGitSshKey.Contracts;

public class ListPublicGitSshKeyQuery
    : ListOfModelQuery<PublicGitSshKeyDto, PublicGitSshKeyId, Guid, ListPublicGitSshKeyQuery>
{
    public Option<UserId> ForUser { get; set; } = Option<UserId>.None;
}
