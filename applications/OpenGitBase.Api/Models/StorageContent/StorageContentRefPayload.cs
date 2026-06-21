namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentRefPayload
{
    public string Name { get; init; } = string.Empty;

    public string CommitSha { get; init; } = string.Empty;
}
