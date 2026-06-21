namespace OpenGitBase.Features.Repository.Contracts;

public sealed class RepositoryReplicationRoutingDto
{
    public long ReplicationEpoch { get; init; }

    public bool WriteQuorumAvailable { get; init; }

    public IReadOnlyList<RepositoryRoutingTargetDto> Targets { get; init; } = [];
}
