namespace OpenGitBase.Api.Models;

public sealed class AdminRepositoryReplicationStatusResponse
{
    public Guid RepositoryId { get; init; }

    public string ReplicationState { get; init; } = string.Empty;

    public long PrimaryWatermark { get; init; }

    public long ReplicationEpoch { get; init; }

    public bool WriteQuorumAvailable { get; init; }

    public IReadOnlyList<AdminRepositoryReplicaStatusResponse> Replicas { get; init; } = [];
}
