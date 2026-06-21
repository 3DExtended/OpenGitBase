namespace OpenGitBase.Features.Repository.Contracts;

public sealed class RepositoryReplicationPeerDto
{
    public Guid StorageNodeId { get; init; }

    public string InternalHost { get; init; } = string.Empty;

    public int InternalHttpPort { get; init; }

    public string Role { get; init; } = string.Empty;

    public bool IsHealthy { get; init; }
}
