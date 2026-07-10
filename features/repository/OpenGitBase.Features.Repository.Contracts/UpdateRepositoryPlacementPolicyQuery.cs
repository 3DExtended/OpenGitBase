using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class UpdateRepositoryPlacementPolicyQuery
    : IQuery<RepositoryDto, UpdateRepositoryPlacementPolicyQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public PlacementPolicy? PlacementPolicy { get; set; }
}
