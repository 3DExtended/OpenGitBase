using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public class UpdateRepositoryDefaultBranchQuery
    : IQuery<RepositoryDto, UpdateRepositoryDefaultBranchQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public string DefaultBranchName { get; set; } = string.Empty;

    /// <summary>
    /// When true, skip branch-exists validation (used when auto-populating from refs).
    /// </summary>
    public bool AllowMissingBranch { get; set; }

    public IReadOnlyList<string>? KnownBranchNames { get; set; }
}
