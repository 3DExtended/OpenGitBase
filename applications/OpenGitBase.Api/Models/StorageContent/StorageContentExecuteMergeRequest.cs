namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentExecuteMergeRequest
{
    public string TargetRef { get; init; } = string.Empty;

    public string SourceRef { get; init; } = string.Empty;

    public string Strategy { get; init; } = string.Empty;

    public string? CommitMessage { get; init; }
}
