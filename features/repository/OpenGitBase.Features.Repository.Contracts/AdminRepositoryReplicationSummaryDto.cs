﻿namespace OpenGitBase.Features.Repository.Contracts;

public sealed class AdminRepositoryReplicationSummaryDto
{
    public Guid RepositoryId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string OwnerSlug { get; init; } = string.Empty;

    public string ReplicationState { get; init; } = string.Empty;

    public int ReplicaCount { get; init; }

    public string PrimaryNodeId { get; init; } = string.Empty;

    public long PrimaryWatermark { get; init; }

    public long MaxWatermarkLag { get; init; }

    public bool WriteQuorumAvailable { get; init; }

    public long ReplicationEpoch { get; init; }

    public DateTimeOffset? OldestLastSyncedAt { get; init; }
}
