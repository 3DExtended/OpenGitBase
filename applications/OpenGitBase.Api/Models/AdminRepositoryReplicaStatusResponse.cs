namespace OpenGitBase.Api.Models;

public sealed class AdminRepositoryReplicaStatusResponse
{
    public Guid StorageNodeId { get; init; }

    public string Role { get; init; } = string.Empty;

    public long AppliedWatermark { get; init; }

    public bool IsInSync { get; init; }

    public DateTimeOffset? LastSyncedAt { get; init; }
}
