namespace OpenGitBase.Features.Repository.Entities;

public class RepositoryReplicaEntity
{
    public Guid RepositoryId { get; set; }

    public Guid StorageNodeId { get; set; }

    public RepositoryReplicaRole Role { get; set; }

    public long AppliedWatermark { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public RepositoryEntity? Repository { get; set; }
}
