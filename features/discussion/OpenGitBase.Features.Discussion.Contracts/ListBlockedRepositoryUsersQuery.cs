using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Discussion.Contracts;

public class ListBlockedRepositoryUsersQuery : IQuery<IReadOnlyList<BlockedUserDto>, ListBlockedRepositoryUsersQuery>
{
    public Guid RepositoryId { get; set; }
}
