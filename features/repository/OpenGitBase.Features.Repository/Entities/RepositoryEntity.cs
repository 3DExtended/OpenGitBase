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

    /// <summary>
    /// Everything that counts against the repository's byte limit: <see cref="GitBytesUsed"/>
    /// plus whatever else is stored for it (Git LFS objects).
    /// </summary>
    public long StorageBytesUsed { get; set; }

    /// <summary>
    /// The git part of <see cref="StorageBytesUsed"/>: the bare repository's size on its primary,
    /// as last measured. A new measurement replaces it inside <see cref="StorageBytesUsed"/>.
    /// </summary>
    public long GitBytesUsed { get; set; }

    public string? DefaultBranchName { get; set; }

    public PlacementPolicy? PlacementPolicy { get; set; }

    public long? MaxBytesOverride { get; set; }
}
