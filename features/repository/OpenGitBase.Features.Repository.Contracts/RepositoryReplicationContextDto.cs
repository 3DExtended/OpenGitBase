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

    /// <summary>
    /// The API understands quorum-replicate's <c>createArtifact</c> (it builds and uploads the
    /// encrypted artifact itself). A post-receive hook that relies on it checks this first: an
    /// older API would ignore the flag and refuse the write for lack of confirmed replicas.
    /// </summary>
    public bool ApiCreatesArtifacts { get; init; }
}
