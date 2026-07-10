using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class GetRepositoryByteOverrideEligibilityQuery
    : IQuery<RepositoryByteOverrideEligibilityDto, GetRepositoryByteOverrideEligibilityQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;
}
