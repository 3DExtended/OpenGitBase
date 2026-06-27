namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentMergeabilityPayload
{
    public string Status { get; init; } = string.Empty;

    public bool CanFastForward { get; init; }

    public bool AlreadyUpToDate { get; init; }
}
