namespace OpenGitBase.Api.Models;

public sealed class RepositoryContentRefDto
{
    public string Name { get; init; } = string.Empty;

    public string CommitSha { get; init; } = string.Empty;
}
