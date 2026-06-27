namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentDiffLinePayload
{
    public string Type { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public int? OldLineNumber { get; init; }

    public int? NewLineNumber { get; init; }
}
