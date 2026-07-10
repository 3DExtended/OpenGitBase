using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Repository.Entities;

public class RepositoryEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid OwnerUserId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string PhysicalPath { get; set; } = string.Empty;

    public Guid? StorageNodeId { get; set; }

    public Guid? PrimaryStorageNodeId { get; set; }

    public Guid? ReadReplicaStorageNodeId { get; set; }

    public long ReplicationEpoch { get; set; } = 1;

    public long PrimaryWatermark { get; set; }

    public ReplicationState ReplicationState { get; set; } = ReplicationState.Rf3Healthy;

    public ICollection<RepositoryReplicaEntity> Replicas { get; set; } = [];

    public bool IsPrivate { get; set; } = false;

    public long StorageBytesUsed { get; set; }

    public string? DefaultBranchName { get; set; }

    public PlacementPolicy? PlacementPolicy { get; set; }

    public long? MaxBytesOverride { get; set; }
}
