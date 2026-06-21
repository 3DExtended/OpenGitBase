namespace OpenGitBase.Api.Models;

public sealed class RepositoryContentEntryDto
{
    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public long? Size { get; init; }
}
