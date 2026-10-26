using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

/// <summary>
/// Records a fresh measurement of a repository's git data (the bare repository on its primary)
/// as the git portion of <c>StorageBytesUsed</c>. Everything else in that counter (Git LFS
/// objects) is kept. Returns <c>StorageBytesUsed</c> after the update.
/// </summary>
public sealed class RecordRepositoryGitBytesQuery : IQuery<long, RecordRepositoryGitBytesQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public long GitBytes { get; set; }
}
