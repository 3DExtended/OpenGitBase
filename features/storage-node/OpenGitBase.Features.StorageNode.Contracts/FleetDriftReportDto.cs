namespace OpenGitBase.Features.StorageNode.Contracts;

/// <summary>
/// A point-in-time cross-reference of every healthy storage node's on-disk inventory against the
/// control-plane <c>RepositoryReplica</c> records, surfacing storage/DB drift in both directions.
/// Unreachable nodes are reported in <see cref="Nodes"/> and excluded from drift detection so a
/// probe failure never masquerades as missing data.
/// </summary>
public sealed class FleetDriftReportDto
{
    public DateTimeOffset GeneratedAt { get; init; }

    public IReadOnlyList<FleetDriftNodeStatusDto> Nodes { get; init; } = [];

    public IReadOnlyList<FleetDriftEntryDto> Drift { get; init; } = [];
}
