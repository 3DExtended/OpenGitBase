namespace OpenGitBase.Api.Models;

public sealed class AdminStorageNodeReplicationSummaryResponse
{
    public Guid StorageNodeId { get; init; }

    public string NodeId { get; init; } = string.Empty;

    public int PrimaryRepositoryCount { get; init; }

    public int ReplicaRepositoryCount { get; init; }

    public bool IsSpare { get; init; }
}
