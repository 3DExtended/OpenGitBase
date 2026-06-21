using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class RepositoryReplicationRoutingQuery
    : IQuery<RepositoryReplicationRoutingDto, RepositoryReplicationRoutingQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;
}
