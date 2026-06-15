using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class GetStorageNodeApiTokenQuery
    : IQuery<string, GetStorageNodeApiTokenQuery>
{
    public StorageNodeId StorageNodeId { get; set; } = default!;
}
