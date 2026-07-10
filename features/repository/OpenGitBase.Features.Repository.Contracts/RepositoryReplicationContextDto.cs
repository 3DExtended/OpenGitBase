namespace OpenGitBase.Features.Repository.Contracts;

public sealed class RepositoryReplicationContextDto
{
    public Guid RepositoryId { get; init; }

    public long ReplicationEpoch { get; init; }

    public long PrimaryWatermark { get; init; }

    public bool IsPrimary { get; init; }

    public string PhysicalPath { get; init; } = string.Empty;

    public string ReplicationState { get; init; } = string.Empty;

    public IReadOnlyList<RepositoryReplicationPeerDto> Peers { get; init; } = [];
}
