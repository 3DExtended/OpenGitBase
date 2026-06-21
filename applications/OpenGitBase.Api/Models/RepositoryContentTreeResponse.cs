namespace OpenGitBase.Api.Models;

public sealed class RepositoryContentTreeResponse
{
    public string Ref { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public IReadOnlyList<RepositoryContentEntryDto> Entries { get; init; } = [];

    public RepositoryReplicationLagDto? ReplicationLag { get; init; }
}
