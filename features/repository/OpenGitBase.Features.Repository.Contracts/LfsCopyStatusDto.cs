namespace OpenGitBase.Features.Repository.Contracts;

public sealed class LfsCopyStatusDto
{
    public int ObjectCount { get; init; }

    public long TotalBytes { get; init; }

    /// <summary>The primary and every replica that holds LFS copies, with what each holds and misses.</summary>
    public IReadOnlyList<LfsNodeCopyStatusDto> Nodes { get; init; } = [];
}
