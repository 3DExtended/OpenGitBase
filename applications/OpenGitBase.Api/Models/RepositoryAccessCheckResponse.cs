namespace OpenGitBase.Api.Models;

public sealed class RepositoryAccessCheckResponse
{
    public bool Allowed { get; init; }
    public Guid? ResolvedUserId { get; init; }
    public Guid? RepositoryId { get; init; }
    public string? EffectiveRole { get; init; }
    public string? Reason { get; init; }

    public string? PhysicalPath { get; init; }

    public string? StorageNodeInternalHost { get; init; }

    public int? StorageNodeInternalSshPort { get; init; }

    public int? StorageNodeInternalGitHttpPort { get; init; }
}
