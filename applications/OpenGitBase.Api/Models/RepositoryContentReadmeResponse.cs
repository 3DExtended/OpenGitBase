namespace OpenGitBase.Api.Models;

public sealed class RepositoryContentReadmeResponse
{
    public string Ref { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string MarkdownSource { get; init; } = string.Empty;

    public RepositoryReplicationLagDto? ReplicationLag { get; init; }
}
