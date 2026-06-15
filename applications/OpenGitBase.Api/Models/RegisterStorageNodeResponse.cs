namespace OpenGitBase.Api.Models;

public sealed class RegisterStorageNodeResponse
{
    public Guid StorageNodeId { get; init; }

    public string ApiToken { get; init; } = string.Empty;

    public int HeartbeatIntervalSeconds { get; init; }
}
