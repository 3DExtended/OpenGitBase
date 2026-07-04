#pragma warning disable SA1412 // Store files as UTF-8 with byte order mark
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1412 // Store files as UTF-8 with byte order mark
namespace OpenGitBase.Api.Models;

public sealed class RepositoryCommitResponse
{
    public string Sha { get; init; } = string.Empty;

    public string ShortSha { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string AuthorName { get; init; } = string.Empty;

    public string AuthoredAt { get; init; } = string.Empty;

    public IReadOnlyList<RepositoryCommitParentResponse> Parents { get; init; } = [];

    public RepositoryCommitStatsResponse? Stats { get; init; }

    public string Kind { get; init; } = string.Empty;

    public IReadOnlyList<MergeRequestDiffFileResponse> DiffFiles { get; init; } = [];

    public IReadOnlyList<RepositoryCommitRootFileResponse> RootFiles { get; init; } = [];

    public RepositoryReplicationLagDto? ReplicationLag { get; init; }
}

public sealed class RepositoryCommitParentResponse
{
    public string Sha { get; init; } = string.Empty;

    public string ShortSha { get; init; } = string.Empty;
}

public sealed class RepositoryCommitStatsResponse
{
    public int FilesChanged { get; init; }

    public int Insertions { get; init; }

    public int Deletions { get; init; }
}

public sealed class RepositoryCommitRootFileResponse
{
    public string Path { get; init; } = string.Empty;

    public string ChangeType { get; init; } = string.Empty;
}
