#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1412 // Store files as UTF-8 with byte order mark
namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentCommitPayload
{
    public string Sha { get; init; } = string.Empty;

    public string ShortSha { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string AuthorName { get; init; } = string.Empty;

    public string AuthoredAt { get; init; } = string.Empty;
}

public sealed class StorageContentCommitsPayload
{
    public string BaseSha { get; init; } = string.Empty;

    public string HeadSha { get; init; } = string.Empty;

    public IReadOnlyList<StorageContentCommitPayload> Commits { get; init; } = [];
}
