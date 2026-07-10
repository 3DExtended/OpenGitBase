using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class UpdateStorageNodeHostingScopeQuery
    : IQuery<StorageNodeDto, UpdateStorageNodeHostingScopeQuery>
{
    public StorageNodeId StorageNodeId { get; set; } = default!;

    public HostingScope HostingScope { get; set; }
}
