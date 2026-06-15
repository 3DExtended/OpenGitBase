﻿namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class RegisterStorageNodeResult
{
    public required StorageNodeId StorageNodeId { get; init; }

    public string ApiToken { get; init; } = string.Empty;

    public int HeartbeatIntervalSeconds { get; init; }
}
