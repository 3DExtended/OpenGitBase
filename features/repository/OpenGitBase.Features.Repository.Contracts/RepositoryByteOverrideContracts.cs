using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class UpdateRepositoryMaxBytesOverrideQuery
    : IQuery<RepositoryDto, UpdateRepositoryMaxBytesOverrideQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public long? MaxBytesOverride { get; set; }
}

public sealed class RepositoryByteOverrideEligibilityDto
{
    public bool Eligible { get; set; }

    public string Reason { get; set; } = string.Empty;

    public long? CurrentOverride { get; set; }

    public long MaxAllowedOverride { get; set; }

    public int OrgContributedNodeCount { get; set; }
}

public sealed class GetRepositoryByteOverrideEligibilityQuery
    : IQuery<RepositoryByteOverrideEligibilityDto, GetRepositoryByteOverrideEligibilityQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;
}
