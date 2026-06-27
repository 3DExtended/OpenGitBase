namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentExecuteMergeResult
{
    public bool Success { get; init; }

    public string? CommitSha { get; init; }

    public string? Strategy { get; init; }

    public string? TargetRef { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public int StatusCode { get; init; }
}
