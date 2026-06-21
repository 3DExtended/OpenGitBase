namespace OpenGitBase.Api.Models;

public sealed class StorageNodeHeartbeatRequest
{
    public string NodeId { get; init; } = string.Empty;

    public long FreeBytesAvailable { get; init; }

    public long TotalBytesAvailable { get; init; }

    public IReadOnlyList<RepositoryWatermarkReport>? RepositoryWatermarks { get; init; }
}
