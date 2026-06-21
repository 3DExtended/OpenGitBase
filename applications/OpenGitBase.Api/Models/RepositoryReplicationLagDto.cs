namespace OpenGitBase.Api.Models;

public sealed class RepositoryReplicationLagDto
{
    public bool Behind { get; init; }

    public string? Message { get; init; }
}
