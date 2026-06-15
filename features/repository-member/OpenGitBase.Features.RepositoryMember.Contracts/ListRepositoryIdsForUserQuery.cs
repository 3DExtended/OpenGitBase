using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember.Contracts;

public class ListRepositoryIdsForUserQuery
    : IQuery<IReadOnlyList<Guid>, ListRepositoryIdsForUserQuery>
{
    public UserId UserId { get; set; } = default!;
}
