using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Repository.Contracts;

public class ListRepositoryQuery
    : ListOfModelQuery<RepositoryDto, RepositoryId, Guid, ListRepositoryQuery>
{
    public UserId OwnerUserId { get; set; } = default!;
}
