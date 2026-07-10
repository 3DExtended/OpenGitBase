using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class UpdateStorageNodeCapacityQuery
    : IQuery<StorageNodeDto, UpdateStorageNodeCapacityQuery>
{
    public StorageNodeId StorageNodeId { get; set; } = default!;

    public long MaxBytes { get; set; }
}
