namespace OpenGitBase.Features.StorageNode.Contracts;

/// <summary>
/// One detected drift between what a storage node physically holds and what the control plane
/// records for it.
/// </summary>
public sealed class FleetDriftEntryDto
{
    public Guid StorageNodeId { get; init; }

    public string NodeId { get; init; } = string.Empty;

    public Guid RepositoryId { get; init; }

    /// <summary>The repository's name, or null when no Repository row exists (fully untracked).</summary>
    public string? RepositoryName { get; init; }

    public FleetDriftKind Kind { get; init; }

    public string Detail { get; init; } = string.Empty;
}
