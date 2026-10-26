namespace OpenGitBase.Api.Models;

/// <summary>A repository's Git LFS objects and how complete each node's copies are (after a cold recovery, the
/// primary's copies are the restore progress).</summary>
public sealed class AdminRepositoryLfsStatusResponse
{
    public int ObjectCount { get; init; }

    public long TotalBytes { get; init; }

    public int MissingCopies { get; init; }

    public IReadOnlyList<AdminLfsNodeStatusResponse> Nodes { get; init; } = [];
}
