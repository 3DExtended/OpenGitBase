namespace OpenGitBase.Api.Models;

public sealed class AdminRepositoryReplicationStatusResponse
{
    public Guid RepositoryId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string OwnerSlug { get; init; } = string.Empty;

    public string ReplicationState { get; init; } = string.Empty;

    public long PrimaryWatermark { get; init; }

    public long ReplicationEpoch { get; init; }

    public bool WriteQuorumAvailable { get; init; }

    public IReadOnlyList<AdminRepositoryReplicaStatusResponse> Replicas { get; init; } = [];
}
