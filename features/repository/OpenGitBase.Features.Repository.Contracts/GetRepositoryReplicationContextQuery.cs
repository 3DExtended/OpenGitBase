using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class GetRepositoryReplicationContextQuery
    : IQuery<RepositoryReplicationContextDto, GetRepositoryReplicationContextQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public StorageNodeId StorageNodeId { get; set; } = default!;
}
