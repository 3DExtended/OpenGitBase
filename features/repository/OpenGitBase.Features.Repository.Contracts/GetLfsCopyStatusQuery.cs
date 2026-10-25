using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

/// <summary>
/// How complete a repository's LFS copies are on each of its nodes, read from the copy rows the reconciler keeps
/// current. After a cold recovery the primary's count is the restore progress. None when the repository is unknown.
/// </summary>
public sealed class GetLfsCopyStatusQuery : IQuery<LfsCopyStatusDto, GetLfsCopyStatusQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;
}
