namespace OpenGitBase.Features.StorageNode.Contracts;

/// <summary>Whether a storage node's on-disk inventory could be probed for the drift report.</summary>
public sealed class FleetDriftNodeStatusDto
{
    public Guid StorageNodeId { get; init; }

    public string NodeId { get; init; } = string.Empty;

    public bool Reachable { get; init; }

    /// <summary>Set when <see cref="Reachable"/> is false: why the probe failed.</summary>
    public string? Error { get; init; }
}
