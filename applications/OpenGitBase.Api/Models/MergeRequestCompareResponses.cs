#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1412 // Store files as UTF-8 with byte order mark
namespace OpenGitBase.Api.Models;

public sealed class MergeRequestChangesResponse
{
    public IReadOnlyList<MergeRequestDiffFileResponse> Files { get; init; } = [];
}

public sealed class MergeRequestDiffFileResponse
{
    public string FilePath { get; init; } = string.Empty;

    public string? OldPath { get; init; }

    public string ChangeType { get; init; } = string.Empty;

    public IReadOnlyList<MergeRequestDiffHunkResponse> Hunks { get; init; } = [];
}

public sealed class MergeRequestDiffHunkResponse
{
    public string Header { get; init; } = string.Empty;

    public IReadOnlyList<MergeRequestDiffLineResponse> Lines { get; init; } = [];
}

public sealed class MergeRequestDiffLineResponse
{
    public int? OldLineNumber { get; init; }

    public int? NewLineNumber { get; init; }

    public string Type { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;
}

public sealed class MergeRequestCommitResponse
{
    public string Sha { get; init; } = string.Empty;

    public string ShortSha { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string AuthorName { get; init; } = string.Empty;

    public string AuthoredAt { get; init; } = string.Empty;
}
