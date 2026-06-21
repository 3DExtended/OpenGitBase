namespace OpenGitBase.Api.Models;

public sealed class RepositoryContentRefsResponse
{
    public IReadOnlyList<RepositoryContentRefDto> Branches { get; init; } = [];

    public IReadOnlyList<RepositoryContentRefDto> Tags { get; init; } = [];

    public string? DefaultRef { get; init; }

    public bool IsEmpty { get; init; }

    public RepositoryReplicationLagDto? ReplicationLag { get; init; }
}
