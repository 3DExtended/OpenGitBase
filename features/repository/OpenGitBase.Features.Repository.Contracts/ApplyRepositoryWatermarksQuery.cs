using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class ApplyRepositoryWatermarksQuery
    : IQuery<ApplyRepositoryWatermarksResult, ApplyRepositoryWatermarksQuery>
{
    public string NodeId { get; set; } = string.Empty;

    public IReadOnlyList<StorageNodeRepositoryWatermark> RepositoryWatermarks { get; set; } = [];
}
