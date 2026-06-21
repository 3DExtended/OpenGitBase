using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed record ReplicaSetSelection(
    StorageNodeDto Primary,
    StorageNodeDto ReplicaA,
    StorageNodeDto ReplicaB
)
{
    public IReadOnlyList<StorageNodeDto> AllNodes => [Primary, ReplicaA, ReplicaB];
}
