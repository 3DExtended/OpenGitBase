namespace OpenGitBase.Features.Repository.Contracts;

public sealed class RepositoryRoutingTargetDto
{
    public Guid StorageNodeId { get; init; }

    public string InternalHost { get; init; } = string.Empty;

    public int InternalSshPort { get; init; }

    public int InternalGitHttpPort { get; init; }

    public int InternalHttpPort { get; init; }

    public string Role { get; init; } = string.Empty;

    public bool IsHealthy { get; init; }

    public bool IsInSync { get; init; }

    public bool IsPrimary { get; init; }
}
