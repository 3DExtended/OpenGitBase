#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1412 // Store files as UTF-8 with byte order mark
namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentCommitDetailPayload
{
    public string Sha { get; init; } = string.Empty;

    public string ShortSha { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string AuthorName { get; init; } = string.Empty;

    public string AuthoredAt { get; init; } = string.Empty;

    public IReadOnlyList<StorageContentCommitParentPayload> Parents { get; init; } = [];

    public StorageContentCommitStatsPayload? Stats { get; init; }

    public string Kind { get; init; } = string.Empty;

    public IReadOnlyList<StorageContentCommitFilePayload> Files { get; init; } = [];
}

public sealed class StorageContentCommitParentPayload
{
    public string Sha { get; init; } = string.Empty;

    public string ShortSha { get; init; } = string.Empty;
}

public sealed class StorageContentCommitStatsPayload
{
    public int FilesChanged { get; init; }

    public int Insertions { get; init; }

    public int Deletions { get; init; }
}

public sealed class StorageContentCommitFilePayload
{
    public string? Path { get; init; }

    public string? ChangeType { get; init; }

    public string? OldPath { get; init; }

    public string? NewPath { get; init; }

    public string? Status { get; init; }

    public IReadOnlyList<StorageContentDiffHunkPayload> Hunks { get; init; } = [];
}
