using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class PromotePrimaryReplicaQuery
    : IQuery<PromotePrimaryReplicaResult, PromotePrimaryReplicaQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;
}
