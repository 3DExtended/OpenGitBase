namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentDiffPayload
{
    public string BaseSha { get; init; } = string.Empty;

    public string HeadSha { get; init; } = string.Empty;

    public IReadOnlyList<StorageContentDiffFilePayload> Files { get; init; } = [];
}
