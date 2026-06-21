namespace OpenGitBase.Features.Repository.Contracts;

public sealed class PromotePrimaryReplicaResult
{
    public bool Promoted { get; init; }

    public Guid? NewPrimaryStorageNodeId { get; init; }

    public long ReplicationEpoch { get; init; }
}
